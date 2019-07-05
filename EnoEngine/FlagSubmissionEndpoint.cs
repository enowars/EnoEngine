using EnoCore;
using EnoCore.Models;
using EnoCore.Models.Json;
using EnoEngine.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnoEngine.FlagSubmission
{
    class FlagSubmissionEndpoint
    {
        private static readonly ConcurrentQueue<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> tcs)> FlagInsertsQueue
            = new ConcurrentQueue<(Flag, long, TaskCompletionSource<FlagSubmissionResult>)>();
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoEngine));
        const int InsertSubmissionsRetries = 16;
        const int InsertSubmissionsBatchSize = 1000;
        readonly CancellationToken Token;
        readonly TcpListener ProductionListener = new TcpListener(IPAddress.IPv6Any, 1337);
        readonly TcpListener DebugListener = new TcpListener(IPAddress.IPv6Any, 1338);
        readonly IServiceProvider ServiceProvider;
        private readonly Task UpdateDatabaseTask;

        public FlagSubmissionEndpoint(IServiceProvider serviceProvider, CancellationToken token)
        {
            Token = token;
            ProductionListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            DebugListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            Token.Register(() => ProductionListener.Stop());
            Token.Register(() => DebugListener.Stop());
            ServiceProvider = serviceProvider;
            UpdateDatabaseTask = Task.Run(async () => await InsertSubmissionsLoop());
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
                    var attackerPrefix = new byte[Program.Configuration.TeamSubnetBytesLength];
                    Array.Copy(attackerAddress, attackerPrefix, Program.Configuration.TeamSubnetBytesLength);
                    var attackerPrefixString = BitConverter.ToString(attackerPrefix);
                   
                    var clientTask = Task.Run(async () =>
                    {
                        using (StreamReader reader = new StreamReader(client.GetStream()))
                        {
                            long teamId;
                            var line = await reader.ReadLineAsync();
                            teamId = Convert.ToInt64(line);
                            await HandleIdentifiedSubmissionClient(client, reader, teamId);
                        }
                    });
                    
                }
            }
            catch (ObjectDisposedException) { }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogFatal(new EnoLogMessage()
                {
                    Module = nameof(FlagSubmission),
                    Function = nameof(RunDebugEndpoint),
                    Message = $"RunDebugEndpoint failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
            Logger.LogInfo(new EnoLogMessage()
            {
                Module = nameof(FlagSubmission),
                Function = nameof(RunDebugEndpoint),
                Message = "RunDebugEndpoint finished"
            });
        }

        public async Task RunProductionEndpoint()
        {
            try
            {
                ProductionListener.Start();
                while (!Token.IsCancellationRequested)
                {
                    var client = await ProductionListener.AcceptTcpClientAsync();
                    var attackerAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();
                    var attackerPrefix = new byte[Program.Configuration.TeamSubnetBytesLength];
                    Array.Copy(attackerAddress, attackerPrefix, Program.Configuration.TeamSubnetBytesLength);
                    var attackerPrefixString = BitConverter.ToString(attackerPrefix);
                    long teamId;
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                        teamId = await db.GetTeamIdByPrefix(attackerPrefixString);
                    }
                    var clientTask = Task.Run(async () =>
                    {
                        using (StreamReader reader = new StreamReader(client.GetStream()))
                        {
                            await HandleIdentifiedSubmissionClient(client, reader, teamId);
                        }
                    });
                }
            }
            catch (ObjectDisposedException) { }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogFatal(new EnoLogMessage()
                {
                    Module = nameof(FlagSubmission),
                    Function = nameof(RunProductionEndpoint),
                    Message = $"RunProductionEndpoint failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
            Logger.LogInfo(new EnoLogMessage()
            {
                Module = nameof(FlagSubmission),
                Function = nameof(RunProductionEndpoint),
                Message = "RunProductionEndpoint finished"
            });
        }

        private static string FormatSubmissionResult(FlagSubmissionResult result)
        {
            switch (result)
            {
                case FlagSubmissionResult.Ok:
                    return "VALID: Flag accepted!";
                case FlagSubmissionResult.Invalid:
                    return "INVALID: You have submitted an invalid string!";
                case FlagSubmissionResult.Duplicate:
                    return "RESUBMIT: You have already sent this flag!";
                case FlagSubmissionResult.Own:
                    return "OWNFLAG: This flag belongs to you!";
                case FlagSubmissionResult.Old:
                    return "OLD: You have submitted an old flag!";
                case FlagSubmissionResult.UnknownError:
                    return "ERROR: An unexpected error occured :(";
                case FlagSubmissionResult.InvalidSenderError:
                    return "ILLEGAL: Your IP address does not belong to any team's subnet!";
                default:
                    return "ERROR: An even more unexpected rrror occured :(";
            }
        }

        public async Task HandleIdentifiedSubmissionClient(TcpClient client, StreamReader reader, long teamId)
        {
            try
            {
                string line = await reader.ReadLineAsync();
                await Task.Delay(1);
                while (!Token.IsCancellationRequested && line != null)
                {
                    var result = await HandleFlagSubmission(line, teamId);
                    var resultArray = Encoding.ASCII.GetBytes(FormatSubmissionResult(result) + "\n");
                    await client.GetStream().WriteAsync(resultArray, 0, resultArray.Length);
                    await client.GetStream().FlushAsync();
                    line = await reader.ReadLineAsync();
                }
            }
            catch (SocketException) { }
            catch (IOException) { }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Module = nameof(CTF),
                    Function = nameof(HandleFlagSubmission),
                    Message = $"HandleIdentifiedSubmissionClient() failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
        }

        private async Task<FlagSubmissionResult> HandleFlagSubmission(string flagString, long attackerTeamId)
        {
            try
            {
                var flag = Flag.FromString(flagString);
                if (flag != null)
                {
                    var tcs = new TaskCompletionSource<FlagSubmissionResult>();
                    while (true)
                    {
                        if (FlagInsertsQueue.Count < 100000)
                        {
                            FlagInsertsQueue.Enqueue((flag, attackerTeamId, tcs));
                            return await tcs.Task;
                        }
                        await Task.Delay(10);
                    }
                }
                return FlagSubmissionResult.Invalid;
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Module = nameof(CTF),
                    Function = nameof(HandleFlagSubmission),
                    Message = $"HandleFlabSubmission() failed: {EnoCoreUtils.FormatException(e)}"
                });
                return FlagSubmissionResult.UnknownError;
            }
        }

        private async Task InsertSubmissionsLoop()
        {
            try
            {
                var lastQueueMessageTimestamp = DateTime.UtcNow;
                Logger.LogStatistics(FlagsubmissionQueueSizeMessage.Create(FlagInsertsQueue.Count));
                while (!Token.IsCancellationRequested)
                {
                    if (DateTime.UtcNow.Subtract(lastQueueMessageTimestamp).Seconds > 5)
                    {
                        Logger.LogStatistics(FlagsubmissionQueueSizeMessage.Create(FlagInsertsQueue.Count));
                        lastQueueMessageTimestamp = DateTime.UtcNow;
                    }
                    var submissions = EnoCoreUtils.DrainQueue(FlagInsertsQueue, InsertSubmissionsBatchSize);
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
                                using (var scope = ServiceProvider.CreateScope())
                                {
                                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                                    await db.ProcessSubmissionsBatch(submissions, Program.Configuration.FlagValidityInRounds);
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            Logger.LogError(new EnoLogMessage()
                            {
                                Module = nameof(FlagSubmissionEndpoint),
                                Function = nameof(InsertSubmissionsLoop),
                                Message = $"InsertSubmissionsLoop dropping batch because: {EnoCoreUtils.FormatException(e)}"
                            });
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
                Logger.LogFatal(new EnoLogMessage()
                {
                    Module = nameof(FlagSubmissionEndpoint),
                    Function = nameof(InsertSubmissionsLoop),
                    Message = $"InsertSubmissionsLoop failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
        }
    }
}
