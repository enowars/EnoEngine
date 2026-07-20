using System.Text.RegularExpressions;
using EnoCore.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace EnoDatabase; // #pragma warning disable SA1118

public record TeamResults(long TeamId, long ServiceId, long RoundId, double AttackPoints, double LostDefensePoints, double ServiceLevelAgreementPoints);
public record Results(long TeamId, long ServiceId, double Points);
public record SLAResults(
    long TeamId,
    long ServiceId,
    double Points,
    TeamServicePointsSnapshot? Snapshot,
    ServiceStatus Status);

public partial class EnoDb
{
    private const double SLA = 100.0;
    private const double ATTACK = 1000.0;
    private const double DEF = -50;

    public string GetQuery(EnoDbContext ctx, long minRoundId, long maxRoundId, double storeWeightFactor, double servicesWeightFactor, long teamId)
    {
        Debug.Assert(storeWeightFactor > 0, "Invalid store weight");
        Debug.Assert(servicesWeightFactor > 0, "Invalid services weight");
        long oldSnapshotRoundId = minRoundId - 1;

        var sw = new Stopwatch();
        sw.Restart();
        var query =
            from team in ctx.Teams
            from service in ctx.Services
            select new
            {
                TeamId = teamId,
                ServiceId = service.Id,
                RoundId = maxRoundId,
                AttackPoints = ctx.SubmittedFlags // service, attacker, round
                    .Where(sf => sf.FlagServiceId == service.Id)
                    .Where(sf => sf.AttackerTeamId == teamId)
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
                            .Where(e => e.TeamId == teamId)
                            .Where(e => e.ServiceId == service.Id)
                            .Single().AttackPoints,
                        0.0),
                LostDefensePoints = (DEF
                    * ctx.Services.Where(e => e.Id == service.Id).Single().WeightFactor / servicesWeightFactor
                    / ctx.Services.Where(e => e.Id == service.Id).Single().FlagsPerRound
                    * ctx.SubmittedFlags // service, owner, round
                        .Where(e => e.FlagServiceId == service.Id)
                        .Where(e => e.FlagOwnerId == teamId)
                        .Where(e => e.FlagRoundId <= maxRoundId)
                        .Where(e => e.FlagRoundId >= minRoundId)
                        .Select(e => new { e.FlagServiceId, e.FlagOwnerId, e.FlagRoundId, e.FlagRoundOffset })
                        .Distinct() // Lost flags
                        .Count())
                    + Math.Min(
                        ctx.TeamServicePointsSnapshot
                            .Where(e => e.RoundId == oldSnapshotRoundId)
                            .Where(e => e.TeamId == teamId)
                            .Where(e => e.ServiceId == service.Id)
                            .Single().LostDefensePoints,
                        0.0),
                ServiceLevelAgreementPoints = ctx.RoundTeamServiceStatus
                    .Where(e => e.GameRoundId <= maxRoundId)
                    .Where(e => e.GameRoundId >= minRoundId)
                    .Where(e => e.TeamId == teamId)
                    .Where(e => e.ServiceId == service.Id)
                    .Sum(sla => SLA
                        * ctx.Services.Where(s => s.Id == s.Id).Single().WeightFactor
                        * (sla.Status == ServiceStatus.OK ? 1 : sla.Status == ServiceStatus.RECOVERING ? 0.5 : 0)
                        / servicesWeightFactor)
                    + Math.Max(
                        ctx.TeamServicePointsSnapshot
                            .Where(e => e.RoundId == oldSnapshotRoundId)
                            .Where(e => e.TeamId == teamId)
                            .Where(e => e.ServiceId == service.Id)
                            .Single().ServiceLevelAgreementPoints,
                        0.0),
                Status = ctx.RoundTeamServiceStatus
                    .Where(e => e.GameRoundId == maxRoundId)
                    .Where(e => e.TeamId == teamId)
                    .Where(e => e.ServiceId == service.Id)
                    .Select(e => e.Status)
                    .Single(),
                ErrorMessage = ctx.RoundTeamServiceStatus
                    .Where(e => e.GameRoundId == maxRoundId)
                    .Where(e => e.TeamId == teamId)
                    .Where(e => e.ServiceId == service.Id)
                    .Select(e => e.ErrorMessage)
                    .Single(),
            };

        var queryString = query.ToQueryString();

        //queryString = queryString.Replace("@__serviceId_0", serviceId.ToString());
        queryString = queryString.Replace("@__teamId_0", teamId.ToString());
        queryString = queryString.Replace("@__maxRoundId_1", maxRoundId.ToString());
        queryString = queryString.Replace("@__minRoundId_2", minRoundId.ToString());
        queryString = queryString.Replace("@__servicesWeightFactor_3", servicesWeightFactor.ToString());
        queryString = queryString.Replace("@__oldSnapshotRoundId_4", (minRoundId - 1).ToString());
        queryString = queryString.Replace("@__storeWeightFactor_5", storeWeightFactor.ToString());
        return queryString;
    }

    public async Task UpdateScores(IDbContextFactory<EnoDbContext> contextFactory, long roundId, Configuration configuration)
    {
        double servicesWeightFactor = await this.context.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor);
        double storeWeightFactor = await this.context.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor * s.FlagVariants);
        var newSnapshotRoundId = roundId - configuration.FlagValidityInRounds - 5;
        var sw = new Stopwatch();
        List<Task> tasks;

        // Phase 2: Create new TeamServicePointsSnapshots, if required
        sw.Restart();
        if (newSnapshotRoundId > 0)
        {
            tasks = new List<Task>();
            foreach (var team in await this.context.Teams.ToArrayAsync()) {
                //foreach (var service in await this.context.Services.ToArrayAsync()) {
                    var ctx = contextFactory.CreateDbContext();
                    var query = this.GetQuery(ctx, newSnapshotRoundId, newSnapshotRoundId, storeWeightFactor, servicesWeightFactor, team.Id);
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
                    tasks.Add(Task.FromResult(async () => {
                        await ctx.Database.ExecuteSqlRawAsync(phase2QueryRaw);
                        ctx.Dispose();
                    }));
                //}
            }
            await Task.WhenAll(tasks);
        }
        Console.WriteLine($"Phase 2 done in {sw.ElapsedMilliseconds}ms");

        // Phase 3: Update TeamServicePoints
        sw.Restart();
        tasks = new List<Task>();
        foreach (var team in await this.context.Teams.ToArrayAsync()) {
            //foreach (var service in await this.context.Services.ToArrayAsync()) {
                var ctx = contextFactory.CreateDbContext();
                var phase3Query = this.GetQuery(ctx, newSnapshotRoundId + 1, roundId, storeWeightFactor, servicesWeightFactor, team.Id);
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
                tasks.Add(Task.FromResult(async () => {
                    await ctx.Database.ExecuteSqlRawAsync(phase3QueryRaw);
                    ctx.Dispose();
                }));
            //}
        }
        await Task.WhenAll(tasks);
        Console.WriteLine($"Phase 3 done in {sw.ElapsedMilliseconds}ms");

        // Phase 4: Update Teams
        sw.Restart();
        foreach (var team in await this.context.Teams.ToArrayAsync())
        {
            team.AttackPoints = await this.context.TeamServicePoints
                .Where(e => e.TeamId == team.Id)
                .Select(e => e.AttackPoints)
                .SumAsync();

            team.DefensePoints = await this.context.TeamServicePoints
                .Where(e => e.TeamId == team.Id)
                .Select(e => e.DefensePoints)
                .SumAsync();

            team.ServiceLevelAgreementPoints = await this.context.TeamServicePoints
                .Where(e => e.TeamId == team.Id)
                .Select(e => e.ServiceLevelAgreementPoints)
                .SumAsync();

            team.TotalPoints = team.AttackPoints + team.DefensePoints + team.ServiceLevelAgreementPoints;
        }

        await this.context.SaveChangesAsync();
        Console.WriteLine($"Phase 4 done in {sw.ElapsedMilliseconds}ms");
    }

    public async Task<Scoreboard> GetCurrentScoreboard(long roundId)
    {
        var sw = new Stopwatch();
        sw.Start();
        this.logger.LogInformation($"{nameof(this.GetCurrentScoreboard)} Started Scoreboard Generation");
        var teams = this.context.Teams
            .Include(t => t.TeamServicePoints)
            .AsNoTracking()
            .OrderByDescending(t => t.TotalPoints)
            .ToList();
        this.logger.LogInformation($"{nameof(this.GetCurrentScoreboard)} Fetched teams after: {sw.ElapsedMilliseconds}ms");
        var round = await this.context.Rounds
            .AsNoTracking()
            .Where(r => r.Id == roundId)
            .FirstOrDefaultAsync();
        this.logger.LogInformation($"{nameof(this.GetCurrentScoreboard)} Fetched round after: {sw.ElapsedMilliseconds}ms");
        var services = this.context.Services
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .ToList();
        this.logger.LogInformation($"{nameof(this.GetCurrentScoreboard)} Fetched services after: {sw.ElapsedMilliseconds}ms");

        var scoreboardTeams = new List<ScoreboardTeam>();
        var scoreboardServices = new List<ScoreboardService>();

        foreach (var service in services)
        {
            var firstBloods = new SubmittedFlag[service.FlagVariants];
            for (int i = 0; i < service.FlagsPerRound; i++)
            {
                var storeId = i % service.FlagVariants;
                var fb = await this.context.SubmittedFlags
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

        this.logger.LogInformation($"{nameof(this.GetCurrentScoreboard)} Iterated services after: {sw.ElapsedMilliseconds}ms");

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

        this.logger.LogInformation($"{nameof(this.GetCurrentScoreboard)} Iterated teams after: {sw.ElapsedMilliseconds}ms");

        var scoreboard = new Scoreboard(
            roundId,
            round?.Begin.ToString(EnoCoreUtil.DateTimeFormat),
            round?.End.ToString(EnoCoreUtil.DateTimeFormat),
            string.Empty, // TODO
            scoreboardServices.ToArray(),
            scoreboardTeams.ToArray());
        this.logger.LogInformation($"{nameof(this.GetCurrentScoreboard)} Finished after: {sw.ElapsedMilliseconds}ms");
        return scoreboard;
    }
}
