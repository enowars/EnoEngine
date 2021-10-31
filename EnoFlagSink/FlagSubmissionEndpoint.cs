namespace EnoFlagSink
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO.Pipelines;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Logging;
    using EnoCore.Models;
    using EnoCore.Models.Database;
    using EnoDatabase;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class FlagSubmissionEndpoint
    {
        private const int MaxLineLength = 200;
        private const int SubmissionBatchSize = 500;
        private const int SubmissionTasks = 4;
        private readonly Dictionary<long, Channel<(byte[] FlagString, Flag Flag, ChannelWriter<(byte[], FlagSubmissionResult)> ResultWriter)>> channels = new();
        private readonly Dictionary<long, TeamFlagSubmissionStatistic> submissionStatistics = new();
        private readonly TcpListener productionListener = new(IPAddress.IPv6Any, 1337);
        private readonly TcpListener debugListener = new(IPAddress.IPv6Any, 1338);
        private readonly ILogger logger;
        private readonly EnoDbUtil databaseUtil;
        private readonly IServiceProvider serviceProvider;
        private readonly EnoStatistics enoStatistics;

        public FlagSubmissionEndpoint(
            IServiceProvider serviceProvider,
            ILogger<FlagSubmissionEndpoint> logger,
            EnoDbUtil databaseUtil,
            EnoStatistics enoStatistics)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.databaseUtil = databaseUtil;
            this.enoStatistics = enoStatistics;
            this.productionListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            this.debugListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
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
                await Task.Delay(5000, token);
            }
        }

        public async Task Start(CancellationToken token)
        {
            // Close the listening sockets if the token is cancelled
            token.Register(() => this.productionListener.Stop());
            token.Register(() => this.debugListener.Stop());

            // Setup statistics for every active team
            Team[] activeTeams;
            Configuration configuration;
            {
                using var scope = this.serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
                activeTeams = await db.RetrieveActiveTeams();
                configuration = await db.RetrieveConfiguration();

                // Start a log submission statistics task for every team
                foreach (var activeTeam in activeTeams)
                {
                    this.submissionStatistics[activeTeam.Id] = new();
                    this.channels[activeTeam.Id] = Channel.CreateBounded<
                        (byte[] FlagString,
                        Flag Flag,
                        ChannelWriter<(byte[],
                        FlagSubmissionResult)> ResultWriter)>(
                        new BoundedChannelOptions(100)
                        {
                            SingleReader = false,
                            SingleWriter = false,
                        });

                    var logTask = Task.Run(
                        async () => await this.LogSubmissionStatistics(
                            activeTeam.Id,
                            activeTeam.Name,
                            token),
                        token);
                }
            }

            // Start n insert tasks
            var tasks = new List<Task>();
            for (int i = 0; i < SubmissionTasks; i++)
            {
                tasks.Add(await Task.Factory.StartNew(async () => await this.InsertSubmissionsLoop(i, configuration, token), token, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default));
            }

            // Start production and debug listeners
            tasks.Add(await Task.Factory.StartNew(async () => await this.RunProductionEndpoint(configuration, token), token, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default));
            tasks.Add(await Task.Factory.StartNew(async () => await this.RunDebugEndpoint(configuration, token), token, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default));
            await Task.WhenAny(tasks);
        }

        private async Task RunDebugEndpoint(Configuration configuration, CancellationToken token)
        {
            this.logger.LogInformation($"{nameof(this.RunDebugEndpoint)} started");
            try
            {
                this.debugListener.Start();
                while (!token.IsCancellationRequested)
                {
                    var client = await this.debugListener.AcceptTcpClientAsync();
                    var handlerTask = Task.Run(async () => await FlagSubmissionClientHandler.HandleDevConnection(
                        this.serviceProvider,
                        configuration.FlagSigningKey,
                        configuration.Encoding,
                        this.channels,
                        this.submissionStatistics,
                        client.Client,
                        token));
                }
            }
            catch (Exception e)
            {
                if (!(e is ObjectDisposedException || e is TaskCanceledException))
                {
                    this.logger.LogCritical($"RunDebugEndpoint failed: {e.ToFancyStringWithCaller()}");
                }
            }

            this.logger.LogInformation("RunDebugEndpoint finished");
        }

        private async Task RunProductionEndpoint(Configuration configuration, CancellationToken token)
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
                        if (client is null)
                        {
                            continue;
                        }

                        var t = Task.Run(
                            async () =>
                            {
                                var attackerAddress = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.GetAddressBytes();
                                var attackerPrefix = new byte[configuration.TeamSubnetBytesLength];
                                Array.Copy(attackerAddress, attackerPrefix, configuration.TeamSubnetBytesLength);
                                var team = await this.databaseUtil.RetryScopedDatabaseAction(
                                    this.serviceProvider,
                                    db => db.GetTeamIdByPrefix(attackerPrefix));
                                if (team != null)
                                {
                                    await FlagSubmissionClientHandler.HandleProdConnection(
                                        this.serviceProvider,
                                        configuration.FlagSigningKey,
                                        configuration.Encoding,
                                        team.Id,
                                        this.channels[team.Id],
                                        this.submissionStatistics[team.Id],
                                        client.Client,
                                        token);
                                }
                                else
                                {
                                    var itemBytes = FlagSubmissionResult.Invalid.ToFeedbackBytes();
                                    await client.Client.SendAsync(itemBytes, SocketFlags.None, token);
                                    client.Close();
                                }
                            },
                            token);
                    }
                    catch (Exception e)
                    {
                        if (e is TaskCanceledException)
                        {
                            throw;
                        }

                        this.logger.LogWarning($"RunProductionEndpoint failed to accept connection: {e.ToFancyStringWithCaller()}");
                    }
                }
            }
            catch (Exception e)
            {
                if (!(e is ObjectDisposedException || e is TaskCanceledException))
                {
                    this.logger.LogCritical($"RunProductionEndpoint failed: {e.ToFancyStringWithCaller()}");
                }
            }

            this.logger.LogInformation("RunProductionEndpoint finished");
        }

        private async Task InsertSubmissionsLoop(int number, Configuration configuration, CancellationToken token)
        {
            this.logger.LogInformation($"{nameof(this.InsertSubmissionsLoop)} {number} started");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool isEmpty = true;
                    List<(byte[] FlagString, Flag Flag, long AttackerTeamId, ChannelWriter<(byte[], FlagSubmissionResult Result)>)> submissions = new();
                    foreach (var (teamid, channel) in this.channels)
                    {
                        int submissionsPerTeam = 0;
                        var reader = channel.Reader;
                        while (submissionsPerTeam < 100 && reader.TryRead(out var item))
                        {
                            isEmpty = false;
                            submissionsPerTeam++;
                            submissions.Add((item.FlagString, item.Flag, teamid, item.ResultWriter));
                            if (submissions.Count > SubmissionBatchSize)
                            {
                                try
                                {
                                    await this.databaseUtil.RetryScopedDatabaseAction(
                                        this.serviceProvider,
                                        db => db.ProcessSubmissionsBatch(submissions, configuration.FlagValidityInRounds, this.enoStatistics));
                                }
                                catch (Exception e)
                                {
                                    this.logger.LogError($"InsertSubmissionsLoop dropping batch because: {e.ToFancyString()}");
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
                        await Task.Delay(10, token);
                    }
                    else if (submissions.Count != 0)
                    {
                        try
                        {
                            using var scope = this.serviceProvider.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
                            await db.ProcessSubmissionsBatch(submissions, configuration.FlagValidityInRounds, this.enoStatistics);
                        }
                        catch (Exception e)
                        {
                            this.logger.LogError($"InsertSubmissionsLoop dropping batch because: {e.ToFancyString()}");
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
                this.logger.LogCritical($"InsertSubmissionsLoop failed: {e.ToFancyStringWithCaller()}");
            }
        }

        public class TeamFlagSubmissionStatistic
        {
#pragma warning disable SA1401 // Fields should be private
            public long OkFlags;
            public long DuplicateFlags;
            public long OldFlags;
            public long InvalidFlags;
            public long OwnFlags;
#pragma warning restore SA1401 // Fields should be private

            internal TeamFlagSubmissionStatistic()
            {
                this.OkFlags = 0;
                this.DuplicateFlags = 0;
                this.OldFlags = 0;
                this.InvalidFlags = 0;
                this.OwnFlags = 0;
            }
        }
    }
}
