using EnoCore.Models.Database;

namespace EnoDatabase;

public record Results(long TeamId, long ServiceId, double Points);
public record SLAResults(long TeamId, long ServiceId, double Points);

public partial class EnoDb
{
    private const double SLA = 100.0;
    private const double ATTACK = 50.0;
    private const double DEF = -50;

    public async Task<Results[]> GetAttackPoints(long minRoundId, long maxRoundId, double servicesWeightFactor)
    {
        var query = this.context.SubmittedFlags
            .Where(e => e.RoundId <= maxRoundId)
            .Where(e => e.RoundId >= minRoundId)
            .GroupBy(submittedFlag => new { submittedFlag.AttackerTeamId, submittedFlag.FlagServiceId }) // all captures by team t in service s
            .Select(attackerTeamGroup =>
                new Results(
                    attackerTeamGroup.Key.AttackerTeamId,
                    attackerTeamGroup.Key.FlagServiceId,
                    attackerTeamGroup.Sum(capture => ATTACK
                        * this.context.Services.Where(e => e.Id == capture.FlagServiceId).Single().WeightFactor
                        / this.context.Services.Where(e => e.Id == capture.FlagServiceId).Single().FlagsPerRound
                        / servicesWeightFactor
                        / this.context.SubmittedFlags // amount of other capturers
                            .Where(e => e.FlagServiceId == capture.FlagServiceId)
                            .Where(e => e.FlagRoundId == capture.FlagRoundId)
                            .Where(e => e.FlagOwnerId == capture.FlagOwnerId)
                            .Where(e => e.FlagRoundOffset == capture.FlagRoundOffset)
                            .Count()
                        / Math.Max(1.0, this.context.RoundTeamServiceStatus // amount of not offline teams, at least 1 TODO: discuss whether not offline teams or all teams
                            .Where(e => e.ServiceId == capture.FlagServiceId)
                            .Where(e => e.GameRoundId == capture.FlagRoundId)
                            .Where(e => e.Status != ServiceStatus.OFFLINE)
                            .Count()))));

        Console.WriteLine($"{minRoundId} {maxRoundId} {servicesWeightFactor}");
        Console.WriteLine(query.ToQueryString());
        return await query.ToArrayAsync();
    }

    public async Task<Results[]> GetDefensePoints(long minRoundId, long maxRoundId, double storeWeightFactor)
    {
        return await this.context.SubmittedFlags
            .Where(e => e.RoundId <= maxRoundId)
            .Where(e => e.RoundId >= minRoundId)
            .GroupBy(submittedFlag => new { submittedFlag.FlagOwnerId, submittedFlag.FlagServiceId })
            .Select(ownerTeamGroup =>
                new Results(
                    ownerTeamGroup.Key.FlagOwnerId,
                    ownerTeamGroup.Key.FlagServiceId,
                    ownerTeamGroup.Sum(capture => DEF
                        * this.context.Services.Where(e => e.Id == capture.FlagServiceId).Single().WeightFactor
                        / storeWeightFactor
                        / this.context.SubmittedFlags
                            .Where(sf => sf.FlagServiceId == capture.FlagServiceId)
                            .Where(sf => sf.FlagRoundOffset == capture.FlagRoundOffset)
                            .Where(sf => sf.RoundId == capture.RoundId)
                            .Count())))
            .ToArrayAsync();
    }

    public async Task<SLAResults[]> GetSLAPoints(long minRoundId, long maxRoundId, double servicesWeightFactor)
    {
        return await this.context.RoundTeamServiceStatus
            .Where(e => e.GameRoundId <= maxRoundId)
            .Where(e => e.GameRoundId >= minRoundId)
            .GroupBy(e => new { e.TeamId, e.ServiceId })
            .Select(teamServiceGroup =>
                new SLAResults(
                    teamServiceGroup.Key.TeamId,
                    teamServiceGroup.Key.ServiceId,
                    teamServiceGroup.Sum(sla => SLA
                        * this.context.Services.Where(s => s.Id == teamServiceGroup.Key.ServiceId).Single().WeightFactor
                        * (sla.Status == ServiceStatus.OK ? 1 : sla.Status == ServiceStatus.RECOVERING ? 0.5 : 0)
                        / servicesWeightFactor)))
                .ToArrayAsync();
    }

    public async Task UpdateScores(long roundId, Configuration configuration)
    {
        var sw = new Stopwatch();
        sw.Start();
        double servicesWeightFactor = await this.context.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor);
        double storeWeightFactor = await this.context.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor * s.FlagVariants);
        var snapshotRoundId = roundId - configuration.CheckedRoundsPerRound - 5;
        var newSnapshotRoundId = snapshotRoundId + 1;

        sw.Restart();
        var recentAttackPoints = await this.GetAttackPoints(newSnapshotRoundId, roundId, servicesWeightFactor);
        Console.WriteLine($"GetAttackPoints {sw.ElapsedMilliseconds}ms");
        sw.Restart();
        var recentDefensePoints = await this.GetDefensePoints(newSnapshotRoundId, roundId, storeWeightFactor);
        Console.WriteLine($"GetDefensePoints {sw.ElapsedMilliseconds}ms");
        sw.Restart();
        var recentSLAPoints = await this.GetSLAPoints(newSnapshotRoundId, roundId, servicesWeightFactor);
        Console.WriteLine($"GetSLAPoints {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Update TeamServicePoints (and prepare new snapshot, if necessary)
        foreach (var sla in recentSLAPoints)
        {
            var tsp = await this.context.TeamServicePoints
                .Where(e => e.TeamId == sla.TeamId)
                .Where(e => e.ServiceId == sla.ServiceId)
                .SingleAsync();

            var snapshot = await this.context.TeamServicePointsSnapshot
                .Where(e => e.TeamId == sla.TeamId)
                .Where(e => e.ServiceId == sla.ServiceId)
                .Where(e => e.RoundId == snapshotRoundId)
                .SingleOrDefaultAsync();

            tsp.ServiceLevelAgreementPoints = sla.Points + (snapshot?.ServiceLevelAgreementPoints ?? 0);
            tsp.Status = await this.context.RoundTeamServiceStatus
                .Where(e => e.TeamId == sla.TeamId)
                .Where(e => e.ServiceId == sla.ServiceId)
                .Where(e => e.GameRoundId == roundId)
                .Select(e => e.Status)
                .SingleAsync();

            if (newSnapshotRoundId > 0)
            {
                this.context.TeamServicePointsSnapshot.Add(
                    new TeamServicePointsSnapshot(
                        tsp.TeamId,
                        tsp.ServiceId,
                        snapshot?.AttackPoints ?? 0,
                        snapshot?.LostDefensePoints ?? 0,
                        snapshot?.ServiceLevelAgreementPoints ?? 0,
                        newSnapshotRoundId));
            }
        }

        Console.WriteLine($"Update SLA {sw.ElapsedMilliseconds}");
        sw.Restart();

        foreach (var attack in recentAttackPoints)
        {
            var tsp = await this.context.TeamServicePoints
                .Where(e => e.TeamId == attack.TeamId)
                .Where(e => e.ServiceId == attack.ServiceId)
                .SingleAsync();

            var snapshot = await this.context.TeamServicePointsSnapshot
                .Where(e => e.TeamId == attack.TeamId)
                .Where(e => e.ServiceId == attack.ServiceId)
                .Where(e => e.RoundId == snapshotRoundId)
                .SingleOrDefaultAsync();

            tsp.AttackPoints = attack.Points + (snapshot?.AttackPoints ?? 0);
        }

        Console.WriteLine($"Update Attack {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        foreach (var def in recentDefensePoints)
        {
            var tsp = await this.context.TeamServicePoints
                .Where(e => e.TeamId == def.TeamId)
                .Where(e => e.ServiceId == def.ServiceId)
                .SingleAsync();

            var snapshot = await this.context.TeamServicePointsSnapshot
                .Where(e => e.TeamId == def.TeamId)
                .Where(e => e.ServiceId == def.ServiceId)
                .Where(e => e.RoundId == snapshotRoundId)
                .SingleOrDefaultAsync();

            tsp.DefensePoints = def.Points + (snapshot?.LostDefensePoints ?? 0);
        }

        Console.WriteLine($"Update Defense {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        await this.context.SaveChangesAsync();

        Console.WriteLine($"SaveChanges {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Update Teams
        var teams = await this.context.Teams.ToArrayAsync();
        foreach (var t in teams)
        {
            t.AttackPoints = this.context.TeamServicePoints
                .Where(e => e.TeamId == t.Id)
                .Select(e => e.AttackPoints)
                .Sum();

            t.ServiceLevelAgreementPoints = this.context.TeamServicePoints
                .Where(e => e.TeamId == t.Id)
                .Select(e => e.ServiceLevelAgreementPoints)
                .Sum();

            t.DefensePoints = this.context.TeamServicePoints
                .Where(e => e.TeamId == t.Id)
                .Select(e => e.DefensePoints)
                .Sum();

            t.TotalPoints = t.AttackPoints + t.ServiceLevelAgreementPoints + t.DefensePoints;
        }

        Console.WriteLine($"Update Teams {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Update new snapshot
        if (newSnapshotRoundId >= 0)
        {
            var stabilizedAttackPoints = await this.GetAttackPoints(newSnapshotRoundId, newSnapshotRoundId, servicesWeightFactor);
            var stabilizedDefensePoints = await this.GetDefensePoints(newSnapshotRoundId, newSnapshotRoundId, storeWeightFactor);
            var stabilizedSLAPoints = await this.GetSLAPoints(newSnapshotRoundId, newSnapshotRoundId, servicesWeightFactor);

            foreach (var attack in stabilizedAttackPoints)
            {
                var newSnapshot = await this.context.TeamServicePointsSnapshot
                    .Where(e => e.TeamId == attack.TeamId)
                    .Where(e => e.ServiceId == attack.ServiceId)
                    .Where(e => e.RoundId == newSnapshotRoundId)
                    .SingleAsync();

                newSnapshot.AttackPoints += attack.Points;
            }

            foreach (var def in stabilizedDefensePoints)
            {
                var newSnapshot = await this.context.TeamServicePointsSnapshot
                    .Where(e => e.TeamId == def.TeamId)
                    .Where(e => e.ServiceId == def.ServiceId)
                    .Where(e => e.RoundId == newSnapshotRoundId)
                    .SingleAsync();

                newSnapshot.LostDefensePoints += def.Points;
            }

            foreach (var sla in stabilizedSLAPoints)
            {
                var newSnapshot = await this.context.TeamServicePointsSnapshot
                    .Where(e => e.TeamId == sla.TeamId)
                    .Where(e => e.ServiceId == sla.ServiceId)
                    .Where(e => e.RoundId == newSnapshotRoundId)
                    .SingleAsync();

                newSnapshot.ServiceLevelAgreementPoints += sla.Points;
            }
        }

        Console.WriteLine($"Update Snapshot {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        await this.context.SaveChangesAsync();

        Console.WriteLine($"SaveChanges {sw.ElapsedMilliseconds}ms");
        sw.Restart();
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
