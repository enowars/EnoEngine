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
    using EnoCore.Configuration;
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
        private readonly ImmutableDictionary<long, Channel<(string FlagString, Flag Flag, ChannelWriter<(string, FlagSubmissionResult)> ResultWriter)>> channels;
        private readonly ImmutableDictionary<long, TeamFlagSubmissionStatistic> submissionStatistics;
        private readonly TcpListener productionListener = new TcpListener(IPAddress.IPv6Any, 1337);
        private readonly TcpListener debugListener = new TcpListener(IPAddress.IPv6Any, 1338);
        private readonly ILogger logger;
        private readonly Configuration configuration;
        private readonly EnoDatabaseUtil databaseUtil;
        private readonly IServiceProvider serviceProvider;
        private readonly EnoStatistics enoStatistics;

        public FlagSubmissionEndpoint(IServiceProvider serviceProvider, ILogger<FlagSubmissionEndpoint> logger, Configuration configuration, EnoDatabaseUtil databaseUtil, EnoStatistics enoStatistics)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.configuration = configuration;
            this.databaseUtil = databaseUtil;
            this.enoStatistics = enoStatistics;
            this.productionListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            this.debugListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

            var channels = new Dictionary<long, Channel<(string FlagString, Flag Flag, ChannelWriter<(string, FlagSubmissionResult)> ResultWriter)>>();
            var submissionStatistics = new Dictionary<long, TeamFlagSubmissionStatistic>();
            foreach (var team in configuration.Teams)
            {
                channels[team.Id] = Channel.CreateBounded<(string FlagString, Flag Flag, ChannelWriter<(string, FlagSubmissionResult)> ResultWriter)>(
                    new BoundedChannelOptions(100) { SingleReader = false, SingleWriter = false });
                submissionStatistics[team.Id] = new TeamFlagSubmissionStatistic(team.Id);
            }

            this.channels = channels.ToImmutableDictionary();
            this.submissionStatistics = submissionStatistics.ToImmutableDictionary();
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

        public async Task Start(Configuration config, CancellationToken token)
        {
            // Close the listening sockets if the token is cancelled
            token.Register(() => this.productionListener.Stop());
            token.Register(() => this.debugListener.Stop());

            // Start a log submission statistics task for every team
            foreach (var team in this.submissionStatistics)
            {
                var name = config.Teams
                        .Where(t => t.Id == team.Key)
                        .First()
                        .Name;
                var t = Task.Run(
                    async () =>
                    await this.LogSubmissionStatistics(
                        team.Key,
                        name,
                        token),
                    token);
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

        private async Task RunDebugEndpoint(Configuration config, CancellationToken token)
        {
            this.logger.LogInformation($"{nameof(this.RunDebugEndpoint)} started");
            try
            {
                this.debugListener.Start();
                while (!token.IsCancellationRequested)
                {
                    var client = await this.debugListener.AcceptTcpClientAsync();
                    await FlagSubmissionClientHandler.HandleDevConnection(
                        this.serviceProvider,
                        Encoding.ASCII.GetBytes(config.FlagSigningKey),
                        config.Encoding,
                        this.channels,
                        this.submissionStatistics,
                        client.Client,
                        token);
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

        private async Task RunProductionEndpoint(Configuration config, CancellationToken token)
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
                                var attackerPrefix = new byte[this.configuration.TeamSubnetBytesLength];
                                Array.Copy(attackerAddress, attackerPrefix, this.configuration.TeamSubnetBytesLength);
                                var team = await this.databaseUtil.RetryScopedDatabaseAction(
                                    this.serviceProvider,
                                    db => db.GetTeamIdByPrefix(attackerPrefix));
                                if (team != null)
                                {
                                    FlagSubmissionClientHandler.HandleProdConnection(
                                        this.serviceProvider,
                                        Encoding.ASCII.GetBytes(this.configuration.FlagSigningKey),
                                        this.configuration.Encoding,
                                        team.Id,
                                        this.channels[team.Id],
                                        this.submissionStatistics[team.Id],
                                        client.Client,
                                        token);
                                }
                                else
                                {
                                    var itemBytes = Encoding.ASCII.GetBytes(FlagSubmissionResult.InvalidSenderError.ToUserFriendlyString());
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

        private async Task InsertSubmissionsLoop(int number, CancellationToken token)
        {
            this.logger.LogInformation($"{nameof(this.InsertSubmissionsLoop)} {number} started");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool isEmpty = true;
                    List<(string FlagString, Flag Flag, long AttackerTeamId, ChannelWriter<(string, FlagSubmissionResult Result)>)> submissions = new();
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
                                        db => db.ProcessSubmissionsBatch(submissions, this.configuration.FlagValidityInRounds, this.enoStatistics));
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
                            await this.databaseUtil.RetryScopedDatabaseAction(
                                this.serviceProvider,
                                db => db.ProcessSubmissionsBatch(submissions, this.configuration.FlagValidityInRounds, this.enoStatistics));
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
            public long TeamId;
            public long OkFlags;
            public long DuplicateFlags;
            public long OldFlags;
            public long InvalidFlags;
            public long OwnFlags;
#pragma warning restore SA1401 // Fields should be private

            internal TeamFlagSubmissionStatistic(long teamId)
            {
                this.TeamId = teamId;
                this.OkFlags = 0;
                this.DuplicateFlags = 0;
                this.OldFlags = 0;
                this.InvalidFlags = 0;
                this.OwnFlags = 0;
            }
        }
    }
}
