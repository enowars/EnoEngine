namespace EnoEngine;

internal partial class EnoEngine
{
    private static readonly CancellationTokenSource EngineCancelSource = new CancellationTokenSource();

    private readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;
    private readonly EnoDbUtil databaseUtil;
    private readonly EnoStatistics statistics;

    public EnoEngine(ILogger<EnoEngine> logger, IServiceProvider serviceProvider, EnoDbUtil databaseUtil, EnoStatistics enoStatistics)
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

    internal async Task RunRecalculation()
    {
        this.logger.LogInformation("RunRecalculation()");
        var lastFinishedRound = await this.databaseUtil.RetryScopedDatabaseAction(db => db.PrepareRecalculation());
        var config = await this.databaseUtil.ExecuteScopedDatabaseAction(db => db.RetrieveConfiguration());

        for (int i = 1; i <= lastFinishedRound.Id; i++)
        {
            await this.HandleRoundEnd(i, config, true);
        }
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

    private async Task AwaitOldRound()
    {
        var lastRound = await this.databaseUtil.RetryScopedDatabaseAction(db => db.GetLastRound());

        if (lastRound != null)
        {
            var span = lastRound.End.Subtract(DateTime.UtcNow);
            if (span.Seconds > 1)
            {
                this.logger.LogInformation($"Sleeping until old round ends ({lastRound.End})");
                await Task.Delay(span);
            }
        }
    }
}
