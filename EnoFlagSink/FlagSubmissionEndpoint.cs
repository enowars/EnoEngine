namespace EnoFlagSink;

public class FlagSubmissionEndpoint
{
    private const int MaxLineLength = 200;
    private const int SubmissionBatchSize = 500;
    private const int SubmissionTasks = 4;
    private readonly Dictionary<long, Channel<FlagSubmissionRequest>> channels = new();
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
                this.channels[activeTeam.Id] = Channel.CreateBounded<FlagSubmissionRequest>(
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
                                this.logger.LogWarning($"Invalid connection from {((IPEndPoint)client.Client.RemoteEndPoint!).Address}");
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
        List<FlagSubmissionRequest> submissions = new();
        try
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var (teamid, channel) in this.channels)
                {
                    int submissionsPerTeam = 0;
                    var reader = channel.Reader;
                    while (submissionsPerTeam < 100 && reader.TryRead(out var item))
                    {
                        submissionsPerTeam++;
                        submissions.Add(item);
                        if (submissions.Count > SubmissionBatchSize)
                        {
                            var orderedSubReqs = submissions.OrderBy(e => e).ToArray();
                            using var scope = this.serviceProvider.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
                            var results = await db.TryProcessSubmissionsBatch(orderedSubReqs, configuration.FlagValidityInRounds, this.enoStatistics);
                            for (int i = 0; i < orderedSubReqs.Length; i++)
                            {
                                var subReq = orderedSubReqs[i];
                                await subReq.Writer.WriteAsync((subReq.FlagString, results[i]), token);
                            }

                            submissions.Clear();
                        }
                    }
                }

                if (submissions.Count == 0)
                {
                    await Task.Delay(10, token);
                }
                else
                {
                    var orderedSubReqs = submissions.OrderBy(e => e).ToArray();
                    using var scope = this.serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
                    var results = await db.TryProcessSubmissionsBatch(orderedSubReqs, configuration.FlagValidityInRounds, this.enoStatistics);
                    for (int i = 0; i < orderedSubReqs.Length; i++)
                    {
                        var subReq = orderedSubReqs[i];
                        await subReq.Writer.WriteAsync((subReq.FlagString, results[i]), token);
                    }

                    submissions.Clear();
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
}
