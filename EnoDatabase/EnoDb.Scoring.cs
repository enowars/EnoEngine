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

    public string GetQuery(long minRoundId, long maxRoundId, double storeWeightFactor, double servicesWeightFactor)
    {
        Debug.Assert(storeWeightFactor > 0, "Invalid store weight");
        Debug.Assert(servicesWeightFactor > 0, "Invalid services weight");
        long oldSnapshotRoundId = minRoundId - 1;
        var query =
            from team in this.context.Teams
            from service in this.context.Services
            select new
            {
                TeamId = team.Id,
                ServiceId = service.Id,
                RoundId = maxRoundId,
                AttackPoints = this.context.SubmittedFlags // service, attacker, round
                    .Where(sf => sf.FlagServiceId == service.Id)
                    .Where(sf => sf.AttackerTeamId == team.Id)
                    .Where(sf => sf.RoundId <= maxRoundId)
                    .Where(sf => sf.RoundId >= minRoundId)
                    .Sum(sf => ATTACK
                        * this.context.Services.Where(e => e.Id == service.Id).Single().WeightFactor / servicesWeightFactor // Service Weight Scaling
                        / this.context.Services.Where(e => e.Id == service.Id).Single().FlagsPerRound
                        / this.context.Services.Where(e => e.Id == service.Id).Single().FlagVariants
                        / this.context.SubmittedFlags // service, owner, round (, offset)
                            .Where(e => e.FlagServiceId == sf.FlagServiceId)
                            .Where(e => e.FlagOwnerId == sf.FlagOwnerId)
                            .Where(e => e.FlagRoundId == sf.FlagRoundId)
                            .Where(e => e.FlagRoundOffset == sf.FlagRoundOffset)
                            .Count() // Other attackers
                        / this.context.Teams.Where(e => e.Active).Count())
                    + Math.Max(
                        this.context.TeamServicePointsSnapshot
                            .Where(e => e.RoundId == oldSnapshotRoundId)
                            .Where(e => e.TeamId == team.Id)
                            .Where(e => e.ServiceId == service.Id)
                            .Single().AttackPoints,
                        0.0),
                LostDefensePoints = (DEF
                    * this.context.Services.Where(e => e.Id == service.Id).Single().WeightFactor / servicesWeightFactor
                    / this.context.Services.Where(e => e.Id == service.Id).Single().FlagsPerRound
                    * this.context.SubmittedFlags // service, owner, round
                        .Where(e => e.FlagServiceId == service.Id)
                        .Where(e => e.FlagOwnerId == team.Id)
                        .Where(e => e.FlagRoundId <= maxRoundId)
                        .Where(e => e.FlagRoundId >= minRoundId)
                        .Select(e => new { e.FlagServiceId, e.FlagOwnerId, e.FlagRoundId, e.FlagRoundOffset })
                        .Distinct() // Lost flags
                        .Count())
                    + Math.Min(
                        this.context.TeamServicePointsSnapshot
                            .Where(e => e.RoundId == oldSnapshotRoundId)
                            .Where(e => e.TeamId == team.Id)
                            .Where(e => e.ServiceId == service.Id)
                            .Single().LostDefensePoints,
                        0.0),
                ServiceLevelAgreementPoints = this.context.RoundTeamServiceStatus
                    .Where(e => e.GameRoundId <= maxRoundId)
                    .Where(e => e.GameRoundId >= minRoundId)
                    .Where(e => e.TeamId == team.Id)
                    .Where(e => e.ServiceId == service.Id)
                    .Sum(sla => SLA
                        * this.context.Services.Where(s => s.Id == s.Id).Single().WeightFactor
                        * (sla.Status == ServiceStatus.OK ? 1 : sla.Status == ServiceStatus.RECOVERING ? 0.5 : 0)
                        / servicesWeightFactor)
                    + Math.Max(
                        this.context.TeamServicePointsSnapshot
                            .Where(e => e.RoundId == oldSnapshotRoundId)
                            .Where(e => e.TeamId == team.Id)
                            .Where(e => e.ServiceId == service.Id)
                            .Single().ServiceLevelAgreementPoints,
                        0.0),
                Status = this.context.RoundTeamServiceStatus
                    .Where(e => e.GameRoundId == maxRoundId)
                    .Where(e => e.TeamId == team.Id)
                    .Where(e => e.ServiceId == service.Id)
                    .Select(e => e.Status)
                    .Single(),
                ErrorMessage = this.context.RoundTeamServiceStatus
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

    public async Task UpdateScores(long roundId, Configuration configuration)
    {
        double servicesWeightFactor = await this.context.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor);
        double storeWeightFactor = await this.context.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor * s.FlagVariants);
        var newSnapshotRoundId = roundId - configuration.FlagValidityInRounds - 5;
        var sw = new Stopwatch();

        // Phase 2: Create new TeamServicePointsSnapshots, if required
        sw.Restart();
        if (newSnapshotRoundId > 0)
        {
            var query = this.GetQuery(newSnapshotRoundId, newSnapshotRoundId, storeWeightFactor, servicesWeightFactor);
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
            await this.context.Database.ExecuteSqlRawAsync(phase2QueryRaw);
        }

        Console.WriteLine($"Phase 2 done in {sw.ElapsedMilliseconds}ms");

        // Phase 3: Update TeamServicePoints
        sw.Restart();
        var phase3Query = this.GetQuery(newSnapshotRoundId + 1, roundId, storeWeightFactor, servicesWeightFactor);
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
        await this.context.Database.ExecuteSqlRawAsync(phase3QueryRaw);
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
