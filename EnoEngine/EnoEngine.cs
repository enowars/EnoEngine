namespace EnoEngine;

internal class EnoEngine
{
    private static readonly CancellationTokenSource EngineCancelSource = new CancellationTokenSource();

    private readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;
    private readonly EnoDbUtil databaseUtil;
    private readonly EnoStatistics statistics;

    internal EnoEngine(ILogger<EnoEngine> logger, IServiceProvider serviceProvider, EnoDbUtil databaseUtil, EnoStatistics enoStatistics)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        this.databaseUtil = databaseUtil;
        this.statistics = enoStatistics;
    }

    internal async Task RunContest()
    {
        // Gracefully shutdown when CTRL+C is invoked
        Console.CancelKeyPress += (s, e) =>
        {
            this.logger.LogInformation("Shutting down EnoEngine");
            e.Cancel = true;
            EngineCancelSource.Cancel();
        };
        var db = this.serviceProvider.CreateScope().ServiceProvider.GetRequiredService<EnoDb>();
        await this.GameLoop();
    }

    private static async Task DelayUntil(DateTime time, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        if (now > time)
        {
            return;
        }

        var diff = time - now;
        await Task.Delay(diff, token);
    }

    private async Task GameLoop()
    {
        try
        {
            // Check if there is an old round running
            await this.AwaitOldRound();
            while (!EngineCancelSource.IsCancellationRequested)
            {
                var end = await this.StartNewRound();
                await DelayUntil(end, EngineCancelSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            this.logger.LogError($"GameLoop failed: {e.ToFancyStringWithCaller()}");
        }

        this.logger.LogInformation("GameLoop finished");
    }

    private async Task<DateTime> StartNewRound()
    {
        DateTime end;
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        this.logger.LogDebug("Starting new Round");
        DateTime begin = DateTime.UtcNow;
        Round newRound;
        Configuration configuration;
        Team[] teams;
        Service[] services;

        // start the next round
        using (var scope = this.serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
            configuration = await db.RetrieveConfiguration();
            double quatherLength = configuration.RoundLengthInSeconds / 4;
            DateTime q2 = begin.AddSeconds(quatherLength);
            DateTime q3 = begin.AddSeconds(quatherLength * 2);
            DateTime q4 = begin.AddSeconds(quatherLength * 3);
            end = begin.AddSeconds(quatherLength * 4);
            newRound = await db.CreateNewRound(begin, q2, q3, q4, end);
            teams = await db.RetrieveActiveTeams();
            services = await db.RetrieveActiveServices();
        }

        this.logger.LogInformation($"CreateNewRound for {newRound.Id} finished ({stopwatch.ElapsedMilliseconds}ms)");

        // insert put tasks
        var insertPutNewFlagsTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertPutFlagsTasks(newRound, teams, services, configuration));
        var insertPutNewNoisesTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertPutNoisesTasks(newRound, teams, services, configuration));
        var insertHavocsTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertHavocsTasks(newRound, teams, services, configuration));

        await insertPutNewFlagsTask;
        await insertPutNewNoisesTask;
        await insertHavocsTask;

        // insert get tasks
        var insertRetrieveCurrentFlagsTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertRetrieveCurrentFlagsTasks(newRound, teams, services, configuration));
        var insertRetrieveOldFlagsTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertRetrieveOldFlagsTasks(newRound, teams, services, configuration));
        var insertGetCurrentNoisesTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertRetrieveCurrentNoisesTasks(newRound, teams, services, configuration));

        await insertRetrieveCurrentFlagsTask;
        await insertRetrieveOldFlagsTask;
        await insertGetCurrentNoisesTask;

        return end;
    }

    private async Task AwaitOldRound()
    {
        var lastRound = await this.databaseUtil.RetryScopedDatabaseAction(db => db.GetLastRound());

        if (lastRound != null)
        {
            var span = lastRound.End!.Value.Subtract(DateTime.UtcNow);
            if (span.Seconds > 1)
            {
                this.logger.LogInformation($"Sleeping until old round ends ({lastRound.End})");
                await Task.Delay(span, EngineCancelSource.Token);
            }
        }
    }
}
