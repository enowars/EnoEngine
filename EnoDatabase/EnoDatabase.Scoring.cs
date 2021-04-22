namespace EnoDatabase
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Configuration;
    using EnoCore.Models;
    using EnoCore.Scoreboard;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    public partial class EnoDatabase : IEnoDatabase
    {
        public async Task<(long NewLatestSnapshotRoundId, long OldSnapshotRoundId, Service[] Services, Team[] Teams)> GetPointCalculationFrame(long roundId, Configuration config)
        {
            var newLatestSnapshotRoundId = await this.context.Rounds
                .Where(r => r.Id <= roundId)
                .OrderByDescending(r => r.Id)
                .Skip((int)config.FlagValidityInRounds + 1)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var oldSnapshotRoundId = await this.context.Rounds
                .Where(r => r.Id <= roundId)
                .OrderByDescending(r => r.Id)
                .Skip((int)config.FlagValidityInRounds + 2)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var services = await this.context.Services
                .AsNoTracking()
                .ToArrayAsync();

            var teams = await this.context.Teams
                .AsNoTracking()
                .ToArrayAsync();

            return (newLatestSnapshotRoundId, oldSnapshotRoundId, services, teams);
        }

        public async Task CalculateTotalPoints()
        {
            var stats = await this.context.TeamServicePoints
                .GroupBy(ss => ss.TeamId)
                .Select(g => new
                {
                    g.Key,
                    AttackPointsSum = g.Sum(s => s.AttackPoints),
                    DefensePointsSum = g.Sum(s => s.DefensePoints),
                    SLAPointsSum = g.Sum(s => s.ServiceLevelAgreementPoints),
                })
                .AsNoTracking()
                .ToDictionaryAsync(ss => ss.Key);
            var dbTeams = await this.context.Teams
                .ToDictionaryAsync(t => t.Id);
            foreach (var sums in stats)
            {
                var team = dbTeams[sums.Key];
                var sum = sums.Value.AttackPointsSum + sums.Value.DefensePointsSum + sums.Value.SLAPointsSum;
                team.AttackPoints = sums.Value.AttackPointsSum;
                team.DefensePoints = sums.Value.DefensePointsSum;
                team.ServiceLevelAgreementPoints = sums.Value.SLAPointsSum;
                team.TotalPoints = sum;
            }

            await this.context.SaveChangesAsync();
        }

        public async Task CalculateTeamServicePoints(
            Team[] teams,
            long roundId,
            Service service,
            long oldSnapshotsRoundId,
            long newLatestSnapshotRoundId)
        {
            Dictionary<long, TeamServicePointsSnapshot>? snapshot = null;
            if (newLatestSnapshotRoundId > 0)
            {
                snapshot = await this.CreateServiceSnapshot(teams, newLatestSnapshotRoundId, service.Id);
                this.context.TeamServicePointsSnapshot.AddRange(snapshot.Values);
            }

            var latestRoundTeamServiceStatus = await this.context.RoundTeamServiceStatus
                .TagWith("CalculateServiceStats:latestServiceStates")
                .Where(rtts => rtts.ServiceId == service.Id)
                .Where(rtts => rtts.GameRoundId == roundId)
                .AsNoTracking()
                .ToDictionaryAsync(rtss => rtss.TeamId);

            var roundTeamServiceStatus = await this.context.RoundTeamServiceStatus
                .TagWith("CalculateServiceStats:volatileServiceStates")
                .Where(rtts => rtts.ServiceId == service.Id)
                .Where(rtts => rtts.GameRoundId > newLatestSnapshotRoundId)
                .Where(rtts => rtts.GameRoundId <= roundId)
                .GroupBy(rtss => new { rtss.TeamId, rtss.Status })
                .Select(rtss => new { rtss.Key, Amount = rtss.Count() })
                .AsNoTracking()
                .ToDictionaryAsync(rtss => rtss.Key);

            // FlagServiceId, FlagRoundId, FlagOwnerId, FlagRoundOffset
            var lostFlags = (await this.context.SubmittedFlags
                .TagWith("CalculateServiceStats:lostFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == service.Id)
                .Where(sf => sf.RoundId > newLatestSnapshotRoundId)
                .Where(sf => sf.RoundId <= roundId)
                .GroupBy(sf => new { sf.FlagServiceId, sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset })
                .Select(g => new { g.Key, Captures = g.Count() }) // Flag -> LossesOfFlag
                .ToArrayAsync())
                .GroupBy(f => f.Key.FlagOwnerId, f => f.Captures) // This is not a SQL GROUP BY
                .ToDictionary(g => g.Key, g => g.AsEnumerable()); // TeamId -> [LossesOfFlag]

            var capturedFlags = (await this.context.SubmittedFlags
                .TagWith("CalculateServiceStats:capturedFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == service.Id)
                .Where(sf => sf.RoundId > newLatestSnapshotRoundId)
                .Where(sf => sf.RoundId <= roundId)
                .ToArrayAsync())
                .GroupBy(sf => sf.AttackerTeamId) // This is not a SQL GROUP BY
                .ToDictionary(sf => sf.Key, sf => sf.AsEnumerable());

            var allCapturesOfFlags = await this.context.SubmittedFlags
                .TagWith("CalculateServiceStats:allCapturesOfFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == service.Id)
                .Where(sf => sf.RoundId > newLatestSnapshotRoundId)
                .Where(sf => sf.RoundId <= roundId)
                .GroupBy(sf => new { sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset })
                .Select(g => new { g.Key, Amount = g.Count() })
                .ToDictionaryAsync(g => g.Key);

            foreach (var team in teams)
            {
                double slaPoints = 0;
                double attackPoints = 0;
                double defPoints = 0;
                if (snapshot != null && snapshot.ContainsKey(team.Id))
                {
                    slaPoints = snapshot[team.Id].ServiceLevelAgreementPoints;
                    attackPoints = snapshot[team.Id].AttackPoints;
                    defPoints = snapshot[team.Id].LostDefensePoints;
                }

                if (roundTeamServiceStatus.TryGetValue(new { TeamId = team.Id, Status = ServiceStatus.OK }, out var oks))
                {
                    slaPoints += oks.Amount * Math.Sqrt(teams.Length);
                }

                if (roundTeamServiceStatus.TryGetValue(new { TeamId = team.Id, Status = ServiceStatus.RECOVERING }, out var recoverings))
                {
                    slaPoints += recoverings.Amount * Math.Sqrt(teams.Length) / 2.0;
                }

                if (lostFlags.TryGetValue(team.Id, out var lostFlagsOfTeam))
                {
                    foreach (var losses in lostFlagsOfTeam)
                    {
                        defPoints -= Math.Pow(losses, 0.75);
                    }
                }

                if (capturedFlags.TryGetValue(team.Id, out var capturedFlagsOfTeam))
                {
                    attackPoints += capturedFlagsOfTeam.Count();
                    foreach (var capture in capturedFlagsOfTeam)
                    {
                        attackPoints += 1.0 / allCapturesOfFlags[new { capture.FlagRoundId, capture.FlagOwnerId, capture.FlagRoundOffset }].Amount;
                    }
                }

                latestRoundTeamServiceStatus.TryGetValue(team.Id, out var status_rtss);
                this.context.TeamServicePoints.Update(new(team.Id,
                    service.Id,
                    attackPoints,
                    defPoints,
                    slaPoints,
                    status_rtss?.Status ?? ServiceStatus.INTERNAL_ERROR,
                    status_rtss?.ErrorMessage));
            }

            await this.context.SaveChangesAsync();
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
                            string.Empty, // TODO
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

        private async Task<Dictionary<long, TeamServicePointsSnapshot>> CreateServiceSnapshot(Team[] teams, long newLatestSnapshotRoundId, long serviceId)
        {
            // TODO delete existing snapshot?
            var oldSnapshot = await this.GetSnapshot(teams, newLatestSnapshotRoundId - 1, serviceId);
            var lostFlags = (await this.context.SubmittedFlags
                .TagWith("CreateSnapshot:lostFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == serviceId)
                .Where(sf => sf.RoundId == newLatestSnapshotRoundId)
                .GroupBy(sf => new { sf.FlagServiceId, sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset })
                .Select(g => new { g.Key, Captures = g.Count() }) // Flag -> LossesOfFlag
                .ToArrayAsync())
                .GroupBy(f => f.Key.FlagOwnerId, f => f.Captures) // This is not a SQL GROUP BY
                .ToDictionary(g => g.Key, g => g.AsEnumerable()); // TeamId -> [LossesOfFlag]

            var capturedFlags = (await this.context.SubmittedFlags
                .TagWith("CalculateServiceStats:capturedFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == serviceId)
                .Where(sf => sf.RoundId == newLatestSnapshotRoundId)
                .ToArrayAsync())
                .GroupBy(sf => sf.AttackerTeamId) // This is not a SQL GROUP BY
                .ToDictionary(sf => sf.Key, sf => sf.AsEnumerable());

            var roundTeamServiceStatus = await this.context.RoundTeamServiceStatus
                .TagWith("CreateSnapshot:serviceStates")
                .Where(rtts => rtts.ServiceId == serviceId)
                .Where(rtts => rtts.GameRoundId == newLatestSnapshotRoundId)
                .AsNoTracking()
                .ToDictionaryAsync(rtss => rtss.TeamId);

            var allCapturesOfFlags = await this.context.SubmittedFlags
                .TagWith("CalculateServiceStats:allCapturesOfFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == serviceId)
                .Where(sf => sf.RoundId == newLatestSnapshotRoundId)
                .GroupBy(sf => new { sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset })
                .Select(g => new { g.Key, Amount = g.Count() })
                .ToDictionaryAsync(g => g.Key);

            Dictionary<long, TeamServicePointsSnapshot> newServiceSnapshots = new();
            foreach (var team in teams)
            {
                double slaPoints = 0;
                double attackPoints = 0;
                double defPoints = 0;
                if (oldSnapshot != null && oldSnapshot.ContainsKey(team.Id))
                {
                    slaPoints = oldSnapshot[team.Id].ServiceLevelAgreementPoints;
                    attackPoints = oldSnapshot[team.Id].AttackPoints;
                    defPoints = oldSnapshot[team.Id].LostDefensePoints;
                }

                if (roundTeamServiceStatus.TryGetValue(team.Id, out var state))
                {
                    if (state.Status == ServiceStatus.OK)
                    {
                        slaPoints += 1.0 * Math.Sqrt(teams.Length);
                    }

                    if (state.Status == ServiceStatus.RECOVERING)
                    {
                        slaPoints += 0.5 * Math.Sqrt(teams.Length);
                    }
                }

                if (lostFlags.TryGetValue(team.Id, out var lostFlagsOfTeam))
                {
                    foreach (var losses in lostFlagsOfTeam)
                    {
                        defPoints -= Math.Pow(losses, 0.75);
                    }
                }

                if (capturedFlags.TryGetValue(team.Id, out var capturedFlagsOfTeam))
                {
                    attackPoints += capturedFlagsOfTeam.Count();
                    foreach (var capture in capturedFlagsOfTeam)
                    {
                        attackPoints += 1.0 / allCapturesOfFlags[new { capture.FlagRoundId, capture.FlagOwnerId, capture.FlagRoundOffset }].Amount;
                    }
                }

                newServiceSnapshots[team.Id] = new(team.Id,
                    serviceId,
                    attackPoints,
                    defPoints,
                    slaPoints,
                    newLatestSnapshotRoundId);
            }

            return newServiceSnapshots;
        }

        private async Task<Dictionary<long, TeamServicePointsSnapshot>> GetSnapshot(Team[] teams, long snapshotRoundId, long serviceId)
        {
            var oldSnapshots = await this.context.TeamServicePointsSnapshot
                .TagWith("CalculateServiceScores:oldSnapshots")
                .Where(sss => sss.ServiceId == serviceId)
                .Where(sss => sss.RoundId == snapshotRoundId)
                .AsNoTracking()
                .ToDictionaryAsync(sss => sss.TeamId);

            if (oldSnapshots.Count == 0 && snapshotRoundId > 0)
            {
                return await this.CreateServiceSnapshot(teams, snapshotRoundId, serviceId);
            }

            return oldSnapshots; // TODO (re)create if not complete
        }
    }
}
