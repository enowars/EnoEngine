using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoCore.Utils;
using EnoDatabase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EnoEngine.FlagSubmission
{
    internal class FlagSubmissionEndpoint
    {
        private const int MaxLineLength = 200;
        private const int SubmissionBatchSize = 500;
        private const int SubmissionTasks = 4;
        private readonly Dictionary<long, Channel<(Flag Flag, TaskCompletionSource<FlagSubmissionResult> FeedbackSource)>> channels = new Dictionary<long, Channel<(Flag, TaskCompletionSource<FlagSubmissionResult>)>>();
        private readonly Dictionary<long, TeamFlagSubmissionStatistic> submissionStatistics = new Dictionary<long, TeamFlagSubmissionStatistic>();
        private readonly TcpListener productionListener = new TcpListener(IPAddress.IPv6Any, 1337);
        private readonly TcpListener debugListener = new TcpListener(IPAddress.IPv6Any, 1338);
        private readonly ILogger logger;
        private readonly JsonConfiguration configuration;
        private readonly IServiceProvider serviceProvider;
        private readonly EnoStatistics enoStatistics;

        public FlagSubmissionEndpoint(IServiceProvider serviceProvider, ILogger<FlagSubmissionEndpoint> logger, JsonConfiguration configuration, EnoStatistics enoStatistics)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.enoStatistics = enoStatistics;
            this.productionListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            this.debugListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            this.serviceProvider = serviceProvider;
            foreach (var team in configuration.Teams)
            {
                this.channels[team.Id] = Channel.CreateBounded<(Flag, TaskCompletionSource<FlagSubmissionResult>)>(new BoundedChannelOptions(100) { SingleReader = false, SingleWriter = false });
                this.submissionStatistics[team.Id] = new TeamFlagSubmissionStatistic(team.Id);
            }
        }

        public async Task LogSubmissionStatistics(long teamId, string teamName, CancellationToken token)
        {
            var statistic = this.submissionStatistics[teamId];
            while (!token.IsCancellationRequested)
            {
                var okFlags = Interlocked.Exchange(ref statistic.OkFlags, 0);
                var oldFlags = Interlocked.Exchange(ref statistic.OldFlags, 0);
                var ownFlags = Interlocked.Exchange(ref statistic.OwnFlags, 0);
                var duplicateFlags = Interlocked.Exchange(ref statistic.DuplicateFlags, 0);
                var invalidFlags = Interlocked.Exchange(ref statistic.InvalidFlags, 0);
                this.enoStatistics.FlagSubmissionStatisticsMessage(teamName, teamId, okFlags, duplicateFlags, oldFlags, invalidFlags, ownFlags);
                await Task.Delay(5000);
            }
        }

        public async Task Start(JsonConfiguration config, CancellationToken token)
        {
            // Close the listening sockets if the token is cancelled
            token.Register(() => this.productionListener.Stop());
            token.Register(() => this.debugListener.Stop());

            // Start a log submission statistics task for every team
            foreach (var team in this.submissionStatistics)
            {
                var t = Task.Run(async () => await this.LogSubmissionStatistics(
                    team.Key,
                    config.Teams.Where(t => t.Id == team.Key).First().Name,
                    token));
            }

            // Start n insert tasks
            var tasks = new List<Task>();
            for (int i = 0; i < SubmissionTasks; i++)
            {
                tasks.Add(await Task.Factory.StartNew(async () => await this.InsertSubmissionsLoop(i, token), token, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default));
            }

            // Start production and debug listeners
            tasks.Add(await Task.Factory.StartNew(async () => await this.RunProductionEndpoint(config, token), token, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default));
            tasks.Add(await Task.Factory.StartNew(async () => await this.RunDebugEndpoint(config, token), token, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default));
            await Task.WhenAny(tasks);
        }

        private async Task ProcessLinesAsync(Socket socket, long? teamId, JsonConfiguration config, CancellationToken token)
        {
            var pipe = new Pipe();
            Channel<Task<FlagSubmissionResult>> feedbackChannel = Channel.CreateUnbounded<Task<FlagSubmissionResult>>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false });
            Task writing = this.FillPipeAsync(socket, pipe.Writer, token);
            Task reading = this.ReadPipeAsync(pipe.Reader, feedbackChannel.Writer, teamId, config, token);
            Task responding = this.RespondAsync(socket, teamId, feedbackChannel.Reader, token);
            await Task.WhenAll(reading, writing, responding);
            socket.Close();
        }

        private async Task FillPipeAsync(Socket socket, PipeWriter writer, CancellationToken token)
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
                    this.logger.LogDebug($"FillPipeAsync failed: {ex.Message}\n{ex.StackTrace}");
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

        private async Task ReadPipeAsync(PipeReader reader, ChannelWriter<Task<FlagSubmissionResult>> feedbackWriter, long? teamId, JsonConfiguration config, CancellationToken token)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;
                while (this.TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    // Process the line.
                    if (teamId != null)
                    {
                        var flag = Flag.Parse(line, Encoding.ASCII.GetBytes(config.FlagSigningKey), config.Encoding, this.logger);
                        var tcs = new TaskCompletionSource<FlagSubmissionResult>();
                        if (flag == null)
                        {
                            await feedbackWriter.WriteAsync(Task.FromResult(FlagSubmissionResult.Invalid));
                        }
                        else if (flag.OwnerId == teamId.Value)
                        {
                            await feedbackWriter.WriteAsync(Task.FromResult(FlagSubmissionResult.Own));
                        }
                        else
                        {
                            var channel = this.channels[teamId.Value];
                            await channel.Writer.WriteAsync((flag, tcs));
                            await feedbackWriter.WriteAsync(tcs.Task);
                        }
                    }
                    else
                    {
                        teamId = int.Parse(Encoding.ASCII.GetString(line.ToArray()));
                    }
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
                if (buffer.Length > MaxLineLength)
                {
                    await feedbackWriter.WriteAsync(Task.FromResult(FlagSubmissionResult.SpamError));
                    break;
                }
            }

            // Mark the PipeReader and channel as complete.
            await reader.CompleteAsync();
            feedbackWriter.Complete();
        }

        private async Task RespondAsync(Socket socket, long? teamId, ChannelReader<Task<FlagSubmissionResult>> feedbackReader, CancellationToken token)
        {
            TeamFlagSubmissionStatistic? statistic = null;
            if (teamId != null)
            {
                statistic = this.submissionStatistics[teamId.Value];
            }

            while (await feedbackReader.WaitToReadAsync(token))
            {
                while (feedbackReader.TryRead(out var itemTask))
                {
                    var item = await itemTask;
                    if (statistic != null)
                    {
                        switch (item)
                        {
                            case FlagSubmissionResult.Ok:
                                Interlocked.Increment(ref statistic.OkFlags);
                                break;
                            case FlagSubmissionResult.Duplicate:
                                Interlocked.Increment(ref statistic.DuplicateFlags);
                                break;
                            case FlagSubmissionResult.Invalid:
                                Interlocked.Increment(ref statistic.InvalidFlags);
                                break;
                            case FlagSubmissionResult.Old:
                                Interlocked.Increment(ref statistic.OldFlags);
                                break;
                            case FlagSubmissionResult.Own:
                                Interlocked.Increment(ref statistic.OwnFlags);
                                break;
                        }
                    }

                    var itemBytes = Encoding.ASCII.GetBytes(this.FormatSubmissionResult(item)); // TODO don't serialize every time
                    await socket.SendAsync(itemBytes, SocketFlags.None, token);                 // TODO enforce batching
                    if (item == FlagSubmissionResult.SpamError)
                    {
                        // https://blog.netherlabs.nl/articles/2009/01/18/the-ultimate-so_linger-page-or-why-is-my-tcp-not-reliable
                        await Task.Delay(1000);
                        socket.Close();
                        break;
                    }
                }
            }
        }

        private bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            // Look for a EOL in the buffer.
            SequencePosition? position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                line = default;
                return false;
            }

            // Skip the line + the \n.
            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        private async Task RunDebugEndpoint(JsonConfiguration config, CancellationToken token)
        {
            this.logger.LogInformation($"{nameof(this.RunDebugEndpoint)} started");
            try
            {
                this.debugListener.Start();
                while (!token.IsCancellationRequested)
                {
                    var client = await this.debugListener.AcceptTcpClientAsync();
                    var task = this.ProcessLinesAsync(client.Client, null, config, token);
                }
            }
            catch (Exception e)
            {
                if (!(e is ObjectDisposedException || e is TaskCanceledException))
                    this.logger.LogCritical($"RunDebugEndpoint failed: {EnoDatabaseUtils.FormatException(e)}");
            }

            this.logger.LogInformation("RunDebugEndpoint finished");
        }

        private async Task RunProductionEndpoint(JsonConfiguration config, CancellationToken token)
        {
            this.logger.LogInformation($"{nameof(this.RunProductionEndpoint)} started");
            try
            {
                this.productionListener.Start();
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await this.productionListener.AcceptTcpClientAsync();
                        var t = Task.Run(async () =>
                        {
                            var attackerAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();
                            var attackerPrefix = new byte[this.configuration.TeamSubnetBytesLength];
                            Array.Copy(attackerAddress, attackerPrefix, this.configuration.TeamSubnetBytesLength);
                            var attackerPrefixString = BitConverter.ToString(attackerPrefix);
                            Team? team = await this.FindTeamBySubnet(attackerPrefixString);
                            if (team != null)
                            {
                                await this.ProcessLinesAsync(client.Client, team.Id, config, token);
                            }
                            else
                            {
                                var itemBytes = Encoding.ASCII.GetBytes(this.FormatSubmissionResult(FlagSubmissionResult.InvalidSenderError)); // TODO don't serialize every time
                                await client.Client.SendAsync(itemBytes, SocketFlags.None, token);
                                client.Close();
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        if (e is TaskCanceledException) throw;
                        this.logger.LogWarning($"RunProductionEndpoint failed to accept connection: {EnoDatabaseUtils.FormatException(e)}");
                    }
                }
            }
            catch (Exception e)
            {
                if (!(e is ObjectDisposedException || e is TaskCanceledException))
                {
                    this.logger.LogCritical($"RunProductionEndpoint failed: {EnoDatabaseUtils.FormatException(e)}");
                }
            }

            this.logger.LogInformation("RunProductionEndpoint finished");
        }

        private async Task<Team?> FindTeamBySubnet(string attackerPrefixString)
        {
            using var scope = this.serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
            return await db.GetTeamIdByPrefix(attackerPrefixString);
        }

        private string FormatSubmissionResult(FlagSubmissionResult result)
        {
            return result switch
            {
                FlagSubmissionResult.Ok => Misc.SubmissionResultOk,
                FlagSubmissionResult.Invalid => Misc.SubmissionResultInvalid,
                FlagSubmissionResult.Duplicate => Misc.SubmissionResultDuplicate,
                FlagSubmissionResult.Own => Misc.SubmissionResultOwn,
                FlagSubmissionResult.Old => Misc.SubmissionResultOld,
                FlagSubmissionResult.UnknownError => Misc.SubmissionResultUnknownError,
                FlagSubmissionResult.InvalidSenderError => Misc.SubmissionResultInvalidSenderError,
                FlagSubmissionResult.SpamError => Misc.SubmissionResultSpamError,
                _ => Misc.SubmissionResultReallyUnknownError,
            };
        }

        private async Task InsertSubmissionsLoop(int number, CancellationToken token)
        {
            this.logger.LogInformation($"{nameof(this.InsertSubmissionsLoop)} {number} started");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool isEmpty = true;
                    List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> result)> submissions = new List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult>)>();
                    foreach (var (teamid, channel) in this.channels)
                    {
                        int submissionsPerTeam = 0;
                        var reader = channel.Reader;
                        while (submissionsPerTeam < 100 && reader.TryRead(out var item))
                        {
                            isEmpty = false;
                            submissionsPerTeam++;
                            submissions.Add((item.Flag, teamid, item.FeedbackSource));
                            if (submissions.Count > SubmissionBatchSize)
                            {
                                try
                                {
                                    await EnoDatabaseUtils.RetryDatabaseAction(async () =>
                                    {
                                        using var scope = this.serviceProvider.CreateScope();
                                        var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                                        await db.ProcessSubmissionsBatch(submissions, this.configuration.FlagValidityInRounds, this.enoStatistics);
                                    });
                                }
                                catch (Exception e)
                                {
                                    this.logger.LogError($"InsertSubmissionsLoop dropping batch because: {EnoDatabaseUtils.FormatException(e)}");
                                    foreach (var (flag, attackerTeamId, tcs) in submissions)
                                    {
                                        tcs.SetResult(FlagSubmissionResult.UnknownError);
                                    }
                                }
                                finally
                                {
                                    submissions.Clear();
                                }
                            }
                        }
                    }

                    if (isEmpty)
                    {
                        await Task.Delay(10);
                    }
                    else if (submissions.Count != 0)
                    {
                        try
                        {
                            await EnoDatabaseUtils.RetryDatabaseAction(async () =>
                            {
                                using var scope = this.serviceProvider.CreateScope();
                                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                                await db.ProcessSubmissionsBatch(submissions, this.configuration.FlagValidityInRounds, this.enoStatistics);
                            });
                        }
                        catch (Exception e)
                        {
                            this.logger.LogError($"InsertSubmissionsLoop dropping batch because: {EnoDatabaseUtils.FormatException(e)}");
                            foreach (var (flag, attackerTeamId, tcs) in submissions)
                            {
                                tcs.SetResult(FlagSubmissionResult.UnknownError);
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                this.logger.LogCritical($"InsertSubmissionsLoop stopped (TaskCanceledException)");
            }
            catch (Exception e)
            {
                this.logger.LogCritical($"InsertSubmissionsLoop failed: {EnoDatabaseUtils.FormatException(e)}");
            }
        }
    }
}
