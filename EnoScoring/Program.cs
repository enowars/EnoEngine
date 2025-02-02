using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EEnoCore.Models.AttackInfo;
using EnoCore;
using EnoCore.Logging;
using EnoCore.Models.Database;
using EnoCore.Models.Scoreboard;
using EnoDatabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EnoScoring;

internal class EnoScoring
{
    private static readonly CancellationTokenSource ScoringCancelSource = new CancellationTokenSource();
    private readonly ILogger<EnoScoring> logger;
    private readonly IDbContextFactory<EnoDbContext> dbContextFactory;
    private readonly EnoStatistics statistics;
    private const double SLA = 100.0;
    private const double ATTACK = 1000.0;
    private const double DEF = -50;

    public EnoScoring(
        ILogger<EnoScoring> logger,
        IDbContextFactory<EnoDbContext> dbContextFactory,
        EnoStatistics statistics)
    {
        this.logger = logger;
        this.dbContextFactory = dbContextFactory;
        this.statistics = statistics;
    }

    public async Task Run()
    {
        logger.LogInformation("EnoScoring starting");
        using var debugCtx = await this.dbContextFactory.CreateDbContextAsync(ScoringCancelSource.Token);
        //await debugCtx.Database.MigrateAsync(ScoringCancelSource.Token);
        await debugCtx.Rounds.Where(e => e.Id > 400).ExecuteUpdateAsync(e => e.SetProperty(e => e.Status, RoundStatus.Finished));

        logger.LogInformation("EnoScoring looping");
        try
        {
            while (!ScoringCancelSource.IsCancellationRequested)
            {
                var round = await this.GetNextRound();
                if (round == null)
                {
                    await Task.Delay(100);
                    continue;
                }

                this.logger.LogInformation("Scoring round {}", round.Id);
                Stopwatch stopWatch = Stopwatch.StartNew();
                var configuration = await this.GetConfiguration();
                await this.DoRoundTeamServiceStates(round.Id, this.statistics);
                await this.DoScores(round.Id, configuration);
                await this.DoAttackInfo(round.Id, configuration);
                await this.DoCurrentScoreboard(round.Id);
                await debugCtx.Rounds.Where(e => e.Id == round.Id).ExecuteUpdateAsync(e => e.SetProperty(e => e.Status, RoundStatus.Scored));
                stopWatch.Stop();
                this.logger.LogInformation("Scoring round {} took {}ms", round.Id, stopWatch.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            this.logger.LogError($"Scoring failed: {e.ToFancyStringWithCaller()}");
        }
        this.logger.LogInformation("EnoScoring starting finished");
    }

    private async Task<Round?> GetNextRound()
    {
        using var ctx = this.dbContextFactory.CreateDbContext();
        return await ctx.Rounds
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Where(e => e.Status == RoundStatus.Finished)
            .FirstOrDefaultAsync(ScoringCancelSource.Token);
    }

    private async Task<Configuration> GetConfiguration()
    {
        using var ctx = this.dbContextFactory.CreateDbContext();
        return await ctx.Configurations.AsNoTracking().SingleAsync();
    }

    #region Phase 1 (Service States)
    /// <summary>
    /// Determine which services were in which state (OK, DOWN, MUMBLE, ...
    /// </summary>
    /// <param name="roundId"></param>
    /// <param name="statistics"></param>
    /// <returns></returns>
    private async Task DoRoundTeamServiceStates(long roundId, EnoStatistics statistics)
    {
        var sw = Stopwatch.StartNew();
        using var ctx = this.dbContextFactory.CreateDbContext();
        await ctx.RoundTeamServiceStatus.Where(e => e.GameRoundId >= roundId).ExecuteDeleteAsync(ScoringCancelSource.Token);
        var teams = await ctx.Teams.AsNoTracking().ToArrayAsync();
        var services = await ctx.Services.AsNoTracking().ToArrayAsync();

        var currentRoundWorstResults = new Dictionary<(long ServiceId, long TeamId), CheckerTask?>();
        var currentTasks = await ctx.CheckerTasks
            .TagWith("CalculateRoundTeamServiceStates:currentRoundTasks")
            .Where(ct => ct.CurrentRoundId == roundId)
            .Where(ct => ct.RelatedRoundId == roundId)
            .OrderBy(ct => ct.CheckerResult)
            .ThenBy(ct => ct.StartTime)
            .AsNoTracking()
            .ToListAsync();
        foreach (var e in currentTasks)
        {
            if (!currentRoundWorstResults.ContainsKey((e.ServiceId, e.TeamId)))
            {
                currentRoundWorstResults[(e.ServiceId, e.TeamId)] = e;
            }
        }

        sw.Stop();
        statistics.LogCheckerTaskAggregateMessage(roundId, sw.ElapsedMilliseconds);

        var oldRoundsWorstResults = await ctx.CheckerTasks
            .TagWith("CalculateRoundTeamServiceStates:oldRoundsTasks")
            .Where(ct => ct.CurrentRoundId == roundId)
            .Where(ct => ct.RelatedRoundId != roundId)
            .GroupBy(ct => new { ct.ServiceId, ct.TeamId })
            .Select(g => new { g.Key, WorstResult = g.Min(ct => ct.CheckerResult) })
            .AsNoTracking()
            .ToDictionaryAsync(g => g.Key, g => g.WorstResult);

        var newRoundTeamServiceStatus = new Dictionary<(long ServiceId, long TeamId), RoundTeamServiceStatus>();
        foreach (var team in teams)
        {
            foreach (var service in services)
            {
                var key2 = (service.Id, team.Id);
                var key = new { ServiceId = service.Id, TeamId = team.Id };
                ServiceStatus status = ServiceStatus.INTERNAL_ERROR;
                string? message = null;
                if (currentRoundWorstResults.ContainsKey(key2))
                {
                    if (currentRoundWorstResults[key2] != null)
                    {
                        status = currentRoundWorstResults[key2]!.CheckerResult.AsServiceStatus();
                        message = currentRoundWorstResults[key2]!.ErrorMessage;
                    }
                    else
                    {
                        status = ServiceStatus.OK;
                        message = null;
                    }
                }

                if (status == ServiceStatus.OK && oldRoundsWorstResults.ContainsKey(key))
                {
                    if (oldRoundsWorstResults[key] != CheckerResult.OK)
                    {
                        status = ServiceStatus.RECOVERING;
                    }
                }

                newRoundTeamServiceStatus[(key.ServiceId, key.TeamId)] = new RoundTeamServiceStatus(
                    status,
                    message,
                    key.TeamId,
                    key.ServiceId,
                    roundId);
            }
        }

        ctx.RoundTeamServiceStatus.AddRange(newRoundTeamServiceStatus.Values);
        await ctx.SaveChangesAsync();
        this.logger.LogInformation($"{nameof(DoRoundTeamServiceStates)} took {sw.ElapsedMilliseconds}ms");
    }
    #endregion

    #region Phase 2 (Scores)
    private async Task DoScores(long roundId, Configuration configuration)
    {
        var sw = Stopwatch.StartNew();
        using var ctx = this.dbContextFactory.CreateDbContext();
        double servicesWeightFactor = await ctx.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor);
        double storeWeightFactor = await ctx.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor * s.FlagVariants);
        var newSnapshotRoundId = roundId - configuration.FlagValidityInRounds - 5;
        await ctx.TeamServicePointsSnapshot.Where(e => e.RoundId >= newSnapshotRoundId).ExecuteDeleteAsync(ScoringCancelSource.Token);

        // Phase 2: Create new TeamServicePointsSnapshots, if required
        sw.Restart();
        if (newSnapshotRoundId > 0)
        {
            var query = this.GetQuery(ctx, newSnapshotRoundId, newSnapshotRoundId, storeWeightFactor, servicesWeightFactor);
            var phase2QueryRaw = @$"
WITH cte AS (
    SELECT ""TeamId"", ""ServiceId"", ""RoundId"", ""AttackPoints"", ""LostDefensePoints"", ""ServiceLevelAgreementPoints""
    FROM (
-----------------
{query}
-----------------
    ) as k
)
INSERT INTO ""TeamServicePointsSnapshot"" (""TeamId"", ""ServiceId"", ""RoundId"", ""AttackPoints"", ""LostDefensePoints"", ""ServiceLevelAgreementPoints"") -- Mind that the order is important!
SELECT * FROM cte
";
            await ctx.Database.ExecuteSqlRawAsync(phase2QueryRaw);
        }

        var phase3Query = this.GetQuery(ctx, newSnapshotRoundId + 1, roundId, storeWeightFactor, servicesWeightFactor);
        var phase3QueryRaw = @$"
WITH cte AS (
-----------------
{phase3Query}
-----------------
)
UPDATE
    ""TeamServicePoints""
SET
    ""AttackPoints"" = cte.""AttackPoints"",
    ""DefensePoints"" = cte.""LostDefensePoints"",
    ""ServiceLevelAgreementPoints"" = cte.""ServiceLevelAgreementPoints"",
    ""Status"" = cte.""Status"",
    ""ErrorMessage"" = cte.""ErrorMessage""
FROM cte
WHERE
    ""TeamServicePoints"".""TeamId"" = cte.""TeamId"" AND
    ""TeamServicePoints"".""ServiceId"" = cte.""ServiceId""
;";
        await ctx.Database.ExecuteSqlRawAsync(phase3QueryRaw);

        foreach (var team in await ctx.Teams.ToArrayAsync())
        {
            team.AttackPoints = await ctx.TeamServicePoints
                .Where(e => e.TeamId == team.Id)
                .Select(e => e.AttackPoints)
                .SumAsync();

            team.DefensePoints = await ctx.TeamServicePoints
                .Where(e => e.TeamId == team.Id)
                .Select(e => e.DefensePoints)
                .SumAsync();

            team.ServiceLevelAgreementPoints = await ctx.TeamServicePoints
                .Where(e => e.TeamId == team.Id)
                .Select(e => e.ServiceLevelAgreementPoints)
                .SumAsync();

            team.TotalPoints = team.AttackPoints + team.DefensePoints + team.ServiceLevelAgreementPoints;
        }

        await ctx.SaveChangesAsync();
        this.logger.LogInformation($"{nameof(DoScores)} took {sw.ElapsedMilliseconds}ms");
    }

    private string GetQuery(EnoDbContext ctx, long minRoundId, long maxRoundId, double storeWeightFactor, double servicesWeightFactor)
    {
        Debug.Assert(storeWeightFactor > 0, "Invalid store weight");
        Debug.Assert(servicesWeightFactor > 0, "Invalid services weight");
        long oldSnapshotRoundId = minRoundId - 1;
        var query =
            from team in ctx.Teams
            from service in ctx.Services
            select new
            {
                TeamId = team.Id,
                ServiceId = service.Id,
                RoundId = maxRoundId,
                AttackPoints = ctx.SubmittedFlags // service, attacker, round
                    .Where(sf => sf.FlagServiceId == service.Id)
                    .Where(sf => sf.AttackerTeamId == team.Id)
                    .Where(sf => sf.RoundId <= maxRoundId)
                    .Where(sf => sf.RoundId >= minRoundId)
                    .Sum(sf => ATTACK
                        * ctx.Services.Where(e => e.Id == service.Id).Single().WeightFactor / servicesWeightFactor // Service Weight Scaling
                        / ctx.Services.Where(e => e.Id == service.Id).Single().FlagsPerRound
                        / ctx.Services.Where(e => e.Id == service.Id).Single().FlagVariants
                        / ctx.SubmittedFlags // service, owner, round (, offset)
                            .Where(e => e.FlagServiceId == sf.FlagServiceId)
                            .Where(e => e.FlagOwnerId == sf.FlagOwnerId)
                            .Where(e => e.FlagRoundId == sf.FlagRoundId)
                            .Where(e => e.FlagRoundOffset == sf.FlagRoundOffset)
                            .Count() // Other attackers
                        / ctx.Teams.Where(e => e.Active).Count())
                    + Math.Max(
                        ctx.TeamServicePointsSnapshot
                            .Where(e => e.RoundId == oldSnapshotRoundId)
                            .Where(e => e.TeamId == team.Id)
                            .Where(e => e.ServiceId == service.Id)
                            .Single().AttackPoints,
                        0.0),
                LostDefensePoints = (DEF
                    * ctx.Services.Where(e => e.Id == service.Id).Single().WeightFactor / servicesWeightFactor
                    / ctx.Services.Where(e => e.Id == service.Id).Single().FlagsPerRound
                    * ctx.SubmittedFlags // service, owner, round
                        .Where(e => e.FlagServiceId == service.Id)
                        .Where(e => e.FlagOwnerId == team.Id)
                        .Where(e => e.FlagRoundId <= maxRoundId)
                        .Where(e => e.FlagRoundId >= minRoundId)
                        .Select(e => new { e.FlagServiceId, e.FlagOwnerId, e.FlagRoundId, e.FlagRoundOffset })
                        .Distinct() // Lost flags
                        .Count())
                    + Math.Min(
                        ctx.TeamServicePointsSnapshot
                            .Where(e => e.RoundId == oldSnapshotRoundId)
                            .Where(e => e.TeamId == team.Id)
                            .Where(e => e.ServiceId == service.Id)
                            .Single().LostDefensePoints,
                        0.0),
                ServiceLevelAgreementPoints = ctx.RoundTeamServiceStatus
                    .Where(e => e.GameRoundId <= maxRoundId)
                    .Where(e => e.GameRoundId >= minRoundId)
                    .Where(e => e.TeamId == team.Id)
                    .Where(e => e.ServiceId == service.Id)
                    .Sum(sla => SLA
                        * ctx.Services.Where(s => s.Id == s.Id).Single().WeightFactor
                        * (sla.Status == ServiceStatus.OK ? 1 : sla.Status == ServiceStatus.RECOVERING ? 0.5 : 0)
                        / servicesWeightFactor)
                    + Math.Max(
                        ctx.TeamServicePointsSnapshot
                            .Where(e => e.RoundId == oldSnapshotRoundId)
                            .Where(e => e.TeamId == team.Id)
                            .Where(e => e.ServiceId == service.Id)
                            .Single().ServiceLevelAgreementPoints,
                        0.0),
                Status = ctx.RoundTeamServiceStatus
                    .Where(e => e.GameRoundId == maxRoundId)
                    .Where(e => e.TeamId == team.Id)
                    .Where(e => e.ServiceId == service.Id)
                    .Select(e => e.Status)
                    .Single(),
                ErrorMessage = ctx.RoundTeamServiceStatus
                    .Where(e => e.GameRoundId == maxRoundId)
                    .Where(e => e.TeamId == team.Id)
                    .Where(e => e.ServiceId == service.Id)
                    .Select(e => e.ErrorMessage)
                    .Single(),
            };

        var queryString = query.ToQueryString();
        queryString = queryString.Replace("@__maxRoundId_0", maxRoundId.ToString());
        queryString = queryString.Replace("@__minRoundId_1", minRoundId.ToString());
        queryString = queryString.Replace("@__servicesWeightFactor_2", servicesWeightFactor.ToString());
        queryString = queryString.Replace("@__oldSnapshotRoundId_3", (minRoundId - 1).ToString());
        queryString = queryString.Replace("@__storeWeightFactor_4", storeWeightFactor.ToString());
        return queryString;
    }
    #endregion

    #region Phase 3 (Attack Info)
    private async Task DoAttackInfo(long roundId, Configuration configuration)
    {
        var sw = Stopwatch.StartNew();
        using var ctx = this.dbContextFactory.CreateDbContext();
        var attackInfo = await this.GetAttackInfo(roundId, configuration.FlagValidityInRounds);
        var json = JsonSerializer.Serialize(attackInfo, EnoCoreUtil.CamelCaseEnumConverterOptions);
        File.WriteAllText($"{EnoCoreUtil.DataDirectory}attack.json", json);
        this.logger.LogInformation($"{nameof(DoAttackInfo)} took {sw.ElapsedMilliseconds}ms");
    }

    private async Task<AttackInfo> GetAttackInfo(long roundId, long flagValidityInRounds)
    {
        using var ctx = await this.dbContextFactory.CreateDbContextAsync();
        var teamAddresses = await ctx.Teams
            .AsNoTracking()
            .Select(t => new { t.Id, t.Address })
            .ToDictionaryAsync(t => t.Id, t => t.Address);
        var availableTeams = await ctx.RoundTeamServiceStatus
            .Where(rtss => rtss.GameRoundId == roundId)
            .GroupBy(rtss => rtss.TeamId)
            .Select(g => new { g.Key, BestResult = g.Min(rtss => rtss.Status) })
            .Where(ts => ts.BestResult < ServiceStatus.OFFLINE)
            .Select(ts => ts.Key)
            .OrderBy(ts => ts)
            .ToArrayAsync();
        var availableTeamAddresses = availableTeams.Select(id => teamAddresses[id] ?? id.ToString()).ToArray();

        var serviceNames = await ctx.Services
            .AsNoTracking()
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name);

        var relevantTasks = await ctx.CheckerTasks
            .AsNoTracking()
            .Where(ct => ct.CurrentRoundId > roundId - flagValidityInRounds)
            .Where(ct => ct.CurrentRoundId <= roundId)
            .Where(ct => ct.Method == CheckerTaskMethod.putflag)
            .Where(ct => ct.AttackInfo != null)
            .Select(ct => new { ct.AttackInfo, ct.VariantId, ct.CurrentRoundId, ct.TeamId, ct.ServiceId })
            .OrderBy(ct => ct.ServiceId)
            .ThenBy(ct => ct.TeamId)
            .ThenBy(ct => ct.CurrentRoundId)
            .ThenBy(ct => ct.VariantId)
            .ToArrayAsync();
        var groupedTasks = relevantTasks
            .GroupBy(ct => new { ct.VariantId, ct.CurrentRoundId, ct.TeamId, ct.ServiceId })
            .GroupBy(g => new { g.Key.CurrentRoundId, g.Key.TeamId, g.Key.ServiceId })
            .GroupBy(g => new { g.Key.TeamId, g.Key.ServiceId })
            .GroupBy(g => new { g.Key.ServiceId });

        var services = new Dictionary<string, AttackInfoService>();
        foreach (var serviceTasks in groupedTasks)
        {
            var service = new AttackInfoService();
            foreach (var teamTasks in serviceTasks)
            {
                var team = new AttackInfoServiceTeam();
                foreach (var roundTasks in teamTasks)
                {
                    var round = new AttackInfoServiceTeamRound();
                    foreach (var variantTasks in roundTasks)
                    {
                        string[] attackInfos = variantTasks.Select(ct => ct.AttackInfo!).ToArray();
                        round.Add(variantTasks.Key.VariantId, attackInfos);
                    }

                    team.Add(roundTasks.Key.CurrentRoundId, round);
                }

                service.TryAdd(teamAddresses[teamTasks.Key.TeamId] ?? teamTasks.Key.TeamId.ToString(), team);
            }

            services.TryAdd(serviceNames[serviceTasks.Key.ServiceId] ?? serviceTasks.Key.ServiceId.ToString(), service);
        }

        var attackInfo = new AttackInfo(availableTeamAddresses, services);
        return attackInfo;
    }
    #endregion

    #region Phase 4 (Scoreboard)
    private async Task DoCurrentScoreboard(long roundId)
    {
        var sw = Stopwatch.StartNew();
        using var ctx = this.dbContextFactory.CreateDbContext();
        var teams = ctx.Teams
            .Include(t => t.TeamServicePoints)
            .AsNoTracking()
            .OrderByDescending(t => t.TotalPoints)
            .ToList();
        var round = await ctx.Rounds
            .AsNoTracking()
            .Where(r => r.Id == roundId)
            .FirstOrDefaultAsync();
        var services = ctx.Services
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .ToList();

        var scoreboardTeams = new List<ScoreboardTeam>();
        var scoreboardServices = new List<ScoreboardService>();

        foreach (var service in services)
        {
            var firstBloods = new SubmittedFlag[service.FlagVariants];
            for (int i = 0; i < service.FlagsPerRound; i++)
            {
                var storeId = i % service.FlagVariants;
                var fb = await ctx.SubmittedFlags
                    .Where(sf => sf.FlagServiceId == service.Id)
                    .Where(sf => sf.FlagRoundOffset == i)
                    .OrderBy(sf => sf.Timestamp)
                    .FirstOrDefaultAsync();

                if (fb is null)
                {
                    continue;
                }

                if (firstBloods[storeId] == null || firstBloods[storeId].Timestamp > fb.Timestamp)
                {
                    firstBloods[storeId] = fb;
                }
            }

            scoreboardServices.Add(new ScoreboardService(
                service.Id,
                service.Name,
                service.FlagsPerRound,
                firstBloods
                    .Where(sf => sf != null)
                    .Select(sf => new ScoreboardFirstBlood(
                        sf.AttackerTeamId,
                        teams.Where(t => t.Id == sf.AttackerTeamId).First().Name,
                        sf.Timestamp.ToString(EnoCoreUtil.DateTimeFormat),
                        sf.RoundId,
                        sf.FlagRoundOffset % service.FlagVariants))
                    .ToArray()));
        }

        foreach (var team in teams)
        {
            scoreboardTeams.Add(new ScoreboardTeam(
                team.Name,
                team.Id,
                team.LogoUrl,
                team.CountryCode,
                team.TotalPoints,
                team.AttackPoints,
                team.DefensePoints,
                team.ServiceLevelAgreementPoints,
                team.TeamServicePoints.Select(
                    tsp => new ScoreboardTeamServiceDetails(
                        tsp.ServiceId,
                        tsp.AttackPoints,
                        tsp.DefensePoints,
                        tsp.ServiceLevelAgreementPoints,
                        tsp.Status,
                        tsp.ErrorMessage))
                .ToArray()));
        }

        var scoreboard = new Scoreboard(
            roundId,
            round?.Begin!.Value.ToString(EnoCoreUtil.DateTimeFormat),
            round?.End!.Value.ToString(EnoCoreUtil.DateTimeFormat),
            string.Empty, // TODO
            scoreboardServices.ToArray(),
            scoreboardTeams.ToArray());

        var json = JsonSerializer.Serialize(scoreboard, EnoCoreUtil.CamelCaseEnumConverterOptions);
        File.WriteAllText($"{EnoCoreUtil.DataDirectory}scoreboard{round!.Id}.json", json);
        File.WriteAllText($"{EnoCoreUtil.DataDirectory}scoreboard.json", json);
        this.logger.LogInformation($"{nameof(this.DoCurrentScoreboard)} took: {sw.ElapsedMilliseconds}ms");
    }
    #endregion

    public static async Task Main(string[] args)
    {
        var rootCommand = new RootCommand("EnoScoring");
        rootCommand.SetHandler(async handler =>
        {
            var serviceProvider = new ServiceCollection()
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                    loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.None);
                    loggingBuilder.AddSimpleConsole(options =>
                    {
                        options.SingleLine = true;
                        options.TimestampFormat = "HH:mm:ss ";
                    });
                    loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoScoring", ScoringCancelSource.Token));
                })
                .AddSingleton<EnoScoring>()
                .AddSingleton(new EnoStatistics(nameof(EnoScoring)))
                .AddPooledDbContextFactory<EnoDbContext>(
                    options =>
                    {
                        options.UseNpgsql(EnoDbContext.PostgresConnectionString);
                    })
                .BuildServiceProvider(validateScopes: true);

            await serviceProvider.GetRequiredService<EnoScoring>().Run();
        });
        await rootCommand.InvokeAsync(args);
    }
}
