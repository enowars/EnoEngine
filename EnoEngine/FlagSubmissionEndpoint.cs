using EnoCore;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.Json;
using EnoEngine.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EnoEngine.FlagSubmission
{
    class FlagSubmissionEndpoint
    {
        private static readonly ConcurrentQueue<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> tcs)> FlagInsertsQueue =
            new ConcurrentQueue<(Flag, long, TaskCompletionSource<FlagSubmissionResult>)>();
        private static readonly Dictionary<long, Channel<(Flag Flag, TaskCompletionSource<FlagSubmissionResult> FeedbackSource)>> Channels =
            new Dictionary<long, Channel<(Flag, TaskCompletionSource<FlagSubmissionResult>)>>();
        private const int InsertSubmissionsBatchSize = 1000;
        private const int MaximumLineLength = 100;
        private static readonly byte[] MaximumLineLengthExceededMessage = Encoding.ASCII.GetBytes("GTFO\n");
        private readonly CancellationToken Token;
        private readonly EnoStatistics Statistics;
        readonly TcpListener ProductionListener = new TcpListener(IPAddress.IPv6Any, 1337);
        readonly TcpListener DebugListener = new TcpListener(IPAddress.IPv6Any, 1338);
        readonly IServiceProvider ServiceProvider;
        private readonly ILogger Logger;

        public FlagSubmissionEndpoint(IServiceProvider serviceProvider, ILogger logger, EnoStatistics statistics, CancellationToken token)
        {
            Logger = logger;
            Token = token;
            Statistics = statistics;
            ProductionListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            DebugListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            Token.Register(() => ProductionListener.Stop());
            Token.Register(() => DebugListener.Stop());
            ServiceProvider = serviceProvider;
            Task.Run(async () => await InsertSubmissionsLoop(), CancellationToken.None);
        }

        async Task ProcessLinesAsync(Socket socket, long teamId, CancellationToken token)
        {
            var pipe = new Pipe();
            Channel<Task<FlagSubmissionResult>> feedbackChannel = Channel.CreateUnbounded<Task<FlagSubmissionResult>>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true });
            Task writing = FillPipeAsync(socket, pipe.Writer, teamId, token);
            Task reading = ReadPipeAsync(socket, pipe.Reader, feedbackChannel.Writer, teamId, token);
            Task responding = RespondAsync(socket, feedbackChannel.Reader, token);
            await Task.WhenAll(reading, writing, responding);
            socket.Close();
        }

        async Task FillPipeAsync(Socket socket, PipeWriter writer, long teamId, CancellationToken token)
        {
            const int minimumBufferSize = 512;
            while (true)
            {
                // Allocate at least 512 bytes from the PipeWriter.
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, token);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket.
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"FillPipeAsync failed: {ex.Message}\n{ex.StackTrace}");
                    break;
                }

                // Make the data available to the PipeReader.
                FlushResult result = await writer.FlushAsync(token);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // By completing PipeWriter, tell the PipeReader that there's no more data coming.
            await writer.CompleteAsync();
        }

        async Task ReadPipeAsync(Socket socket, PipeReader reader, ChannelWriter<Task<FlagSubmissionResult>> feedbackWriter, long teamId, CancellationToken token)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    // Process the line.
                    await ProcessLine(line, teamId, feedbackWriter);
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    break;
                }

                // TryReadLine has returned false, so the remaining buffer does not contain a \n.
                // If the length is longer than a flag, somebody is sending bullshit!
                if (buffer.Length > MaximumLineLength)
                {
                    await feedbackWriter.WriteAsync(Task.FromResult(FlagSubmissionResult.SpamError));
                    break;
                }
            }
            // Mark the PipeReader as complete.
            await reader.CompleteAsync();
        }

        private async Task RespondAsync(Socket socket, ChannelReader<Task<FlagSubmissionResult>> feedbackReader, CancellationToken token)
        {
            while (await feedbackReader.WaitToReadAsync(token))
            {
                while (feedbackReader.TryRead(out var itemTask))
                {
                    var item = await itemTask;
                    var itemBytes = Encoding.ASCII.GetBytes(FormatSubmissionResult(item)); //TODO don't serialize every time
                    await socket.SendAsync(itemBytes, SocketFlags.None, token);  //TODO enforce batching
                    if (item == FlagSubmissionResult.SpamError)
                    {
                        socket.Close();
                        break;
                    }
                }
            }
        }

        private async ValueTask ProcessLine(ReadOnlySequence<byte> line, long teamId, ChannelWriter<Task<FlagSubmissionResult>> feedbackWriter)
        {
            var flag = Flag.Parse(line);
            var tcs = new TaskCompletionSource<FlagSubmissionResult>();
            if (flag != null && flag.OwnerId != teamId)
            {
                var channel = Channels[teamId];
                await channel.Writer.WriteAsync((flag, tcs));
                await feedbackWriter.WriteAsync(tcs.Task);
            }
        }

        bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            // Look for a EOL in the buffer.
            SequencePosition? position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                if (buffer.Length > MaximumLineLength)
                {
                    throw new InvalidOperationException("That's not what 1 line per flag looks like");
                }
                line = default;
                return false;
            }

            // Skip the line + the \n.
            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        public async Task RunDebugEndpoint()
        {
            try
            {
                DebugListener.Start();
                while (!Token.IsCancellationRequested)
                {
                    var client = await DebugListener.AcceptTcpClientAsync();
                    var attackerAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();
                    var attackerPrefix = new byte[EnoEngine.Configuration.TeamSubnetBytesLength];
                    Array.Copy(attackerAddress, attackerPrefix, EnoEngine.Configuration.TeamSubnetBytesLength);
                    var attackerPrefixString = BitConverter.ToString(attackerPrefix);

                    throw new NotImplementedException();
                    //var clientTask = Task.Run(async () =>
                    //{
                        //await ProcessLinesAsync(client.Client, teamId, Token); //TODO
                    //});
                }
            }
            catch (ObjectDisposedException) { }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogCritical($"RunDebugEndpoint failed: {EnoCoreUtils.FormatException(e)}");
            }
            Logger.LogInformation("RunDebugEndpoint finished");
        }

        public async Task RunProductionEndpoint()
        {
            try
            {
                ProductionListener.Start();
                while (!Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await ProductionListener.AcceptTcpClientAsync();
                        var attackerAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();
                        var attackerPrefix = new byte[EnoEngine.Configuration.TeamSubnetBytesLength];
                        Array.Copy(attackerAddress, attackerPrefix, EnoEngine.Configuration.TeamSubnetBytesLength);
                        var attackerPrefixString = BitConverter.ToString(attackerPrefix);
                        long teamId;
                        using (var scope = ServiceProvider.CreateScope())
                        {
                            var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                            teamId = await db.GetTeamIdByPrefix(attackerPrefixString);
                        }
                        await ProcessLinesAsync(client.Client, teamId, Token);
                    }
                    catch (TaskCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning($"RunProductionEndpoint failed to handle connection: {EnoCoreUtils.FormatException(e)}");
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogCritical($"RunProductionEndpoint failed: {EnoCoreUtils.FormatException(e)}");
            }
            Logger.LogInformation("RunProductionEndpoint finished");
        }

        private static string FormatSubmissionResult(FlagSubmissionResult result)
        {
            return result switch
            {
                FlagSubmissionResult.Ok => "VALID: Flag accepted!\n",
                FlagSubmissionResult.Invalid => "INVALID: You have submitted an invalid string!\n",
                FlagSubmissionResult.Duplicate => "RESUBMIT: You have already sent this flag!\n",
                FlagSubmissionResult.Own => "OWNFLAG: This flag belongs to you!\n",
                FlagSubmissionResult.Old => "OLD: You have submitted an old flag!\n",
                FlagSubmissionResult.UnknownError => "ERROR: An unexpected error occured :(\n",
                FlagSubmissionResult.InvalidSenderError => "ILLEGAL: Your IP address does not belong to any team's subnet!\n",
                FlagSubmissionResult.SpamError => "SPAM: You should send 1 flag per line!\n",
                _ => "ERROR: An even more unexpected error occured :(\n",
            };
        }

        async Task InsertSubmissionsLoop()
        {
            try
            {
                while (!Token.IsCancellationRequested)
                {
                    List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> result)> submissions = new List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult>)>();
                    foreach (var (teamid, channel) in Channels)
                    {
                        var reader = channel.Reader;
                        while (await reader.WaitToReadAsync(Token))
                        {
                            while (reader.TryRead(out var item))
                            {
                                submissions.Add((item.Flag, teamid, item.FeedbackSource));
                            }
                        }
                    }
                    if (submissions.Count == 0)
                    {
                        await Task.Delay(10);
                    }
                    else
                    {
                        try
                        {
                            await EnoCoreUtils.RetryDatabaseAction(async () =>
                            {
                                using var scope = ServiceProvider.CreateScope();
                                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                                await db.ProcessSubmissionsBatch(submissions, EnoEngine.Configuration.FlagValidityInRounds, Statistics);
                            });
                        }
                        catch (Exception e)
                        {
                            Logger.LogError($"InsertSubmissionsLoop dropping batch because: {EnoCoreUtils.FormatException(e)}");
                            foreach (var (flag, attackerTeamId, tcs) in submissions)
                            {
                                var t = Task.Run(() => tcs.TrySetResult(FlagSubmissionResult.UnknownError));
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogCritical($"InsertSubmissionsLoop failed: {EnoCoreUtils.FormatException(e)}");
            }
        }
    }
}
