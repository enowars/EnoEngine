namespace EnoDatabase;

public partial class EnoDb
{
    private const double SLA = 100.0;
    private const double ATTACK = 50.0;
    private const double DEF = -50;

    public async Task UpdateScores()
    {
        double servicesWeightFactor = await this.context.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor);
        double storeWeightFactor = await this.context.Services.Where(s => s.Active).SumAsync(s => s.WeightFactor * s.FlagVariants);

        var attackPointsQuery = this.context.SubmittedFlags
            .GroupBy(submittedFlag => new { submittedFlag.AttackerTeamId, submittedFlag.FlagServiceId }) // all captures by team t in service s
            .Select(attackerTeamGroup => new
            {
                teamId = attackerTeamGroup.Key.AttackerTeamId,
                serviceId = attackerTeamGroup.Key.FlagServiceId,
                attackPoints = attackerTeamGroup.Sum(capture => ATTACK
                    * this.context.Services.Where(e => e.Id == capture.FlagServiceId).Single().WeightFactor
                    / storeWeightFactor
                    / this.context.SubmittedFlags // amount of other capturers
                        .Where(e => e.FlagServiceId == capture.FlagServiceId)
                        .Where(e => e.FlagRoundId == capture.FlagRoundId)
                        .Where(e => e.FlagOwnerId == capture.FlagOwnerId)
                        .Where(e => e.FlagRoundOffset == capture.FlagRoundOffset)
                        .Count()
                    / this.context.RoundTeamServiceStatus // amount of ok or recovering teams
                        .Where(e => e.ServiceId == capture.FlagServiceId)
                        .Where(e => e.GameRoundId == capture.FlagRoundId)
                        .Where(e => e.Status == ServiceStatus.OK || e.Status == ServiceStatus.RECOVERING)
                        .Count()),
            });

        var slaPointsQuery = this.context.RoundTeamServiceStatus
            .GroupBy(e => new { e.TeamId, e.ServiceId })
            .Select(teamServiceGroup => new
            {
                teamId = teamServiceGroup.Key.TeamId,
                serviceId = teamServiceGroup.Key.ServiceId,
                slaPoints = SLA
                    * this.context.Services.Where(s => s.Id == teamServiceGroup.Key.ServiceId).Single().WeightFactor
                    * teamServiceGroup.Sum(status => status.Status == ServiceStatus.OK ? 1 : status.Status == ServiceStatus.RECOVERING ? 0.5 : 0)
                    / servicesWeightFactor,
            });

        var defPointsQuery = this.context.SubmittedFlags
            .GroupBy(submittedFlag => new { submittedFlag.FlagOwnerId, submittedFlag.FlagServiceId })
            .Select(ownerTeamGroup => new
            {
                teamId = ownerTeamGroup.Key.FlagOwnerId,
                serviceId = ownerTeamGroup.Key.FlagServiceId,
                defPoints = ownerTeamGroup.Sum(capture => DEF
                    * this.context.Services.Where(e => e.Id == capture.FlagServiceId).Single().WeightFactor
                    / storeWeightFactor
                    / this.context.SubmittedFlags
                        .Where(e => e.FlagServiceId == capture.FlagServiceId)
                        .Where(e => e.FlagRoundId == capture.FlagRoundId)
                        .Where(e => e.FlagRoundOffset == capture.FlagRoundOffset)
                        .Select(e => e.FlagOwnerId)
                        .Distinct()
                        .Count()),
            });

        Console.WriteLine("\n##### Attack Points:");
        Console.WriteLine(attackPointsQuery.ToQueryString());
        Console.WriteLine("\n##### SLA Points:");
        Console.WriteLine(slaPointsQuery.ToQueryString());
        Console.WriteLine("\n##### Def Points:");
        Console.WriteLine(defPointsQuery.ToQueryString());
        var attackPoints = await attackPointsQuery.ToArrayAsync();
        var slaPoints = await slaPointsQuery.ToArrayAsync();
        var defPoints = await defPointsQuery.ToArrayAsync();
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
