using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnoDatabase
{
    public partial class EnoDatabase : IEnoDatabase
    {
        public async Task<(long newLatestSnapshotRoundId, long oldSnapshotRoundId, Service[] services, Team[] teams)> GetPointCalculationFrame(long roundId, JsonConfiguration config)
        {
            var newLatestSnapshotRoundId = await _context.Rounds
                .Where(r => r.Id <= roundId)
                .OrderByDescending(r => r.Id)
                .Skip((int)config.FlagValidityInRounds + 1)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var oldSnapshotRoundId = await _context.Rounds
                .Where(r => r.Id <= roundId)
                .OrderByDescending(r => r.Id)
                .Skip((int)config.FlagValidityInRounds + 2)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var services = await _context.Services
                .AsNoTracking()
                .ToArrayAsync();

            var teams = await _context.Teams
                .AsNoTracking()
                .ToArrayAsync();

            return (newLatestSnapshotRoundId, oldSnapshotRoundId, services, teams);
        }

        public async Task CalculateTotalPoints()
        {
            var stats = await _context.ServiceStats
                .GroupBy(ss => ss.TeamId)
                .Select(g => new
                {
                    g.Key,
                    AttackPointsSum = g.Sum(s => s.AttackPoints),
                    DefensePointsSum = g.Sum(s => s.LostDefensePoints),
                    SLAPointsSum = g.Sum(s => s.ServiceLevelAgreementPoints)
                })
                .AsNoTracking()
                .ToDictionaryAsync(ss => ss.Key);
            var dbTeams = await _context.Teams
                .ToDictionaryAsync(t => t.Id);
            foreach (var sums in stats)
            {
                var team = dbTeams[sums.Key];
                var sum = sums.Value.AttackPointsSum + sums.Value.DefensePointsSum + sums.Value.SLAPointsSum;
                team.AttackPoints = sums.Value.AttackPointsSum;
                team.LostDefensePoints = sums.Value.DefensePointsSum;
                team.ServiceLevelAgreementPoints = sums.Value.SLAPointsSum;
                team.TotalPoints = sum;
            }
            await _context.SaveChangesAsync();
        }

        public async Task CalculateServiceStats(Team[] teams, long roundId,
            Service service, long oldSnapshotsRoundId, long newLatestSnapshotRoundId)
        {
            Dictionary<long, ServiceStatsSnapshot>? snapshot = null;
            if (newLatestSnapshotRoundId > 0)
            {
                snapshot = await CreateServiceSnapshot(teams, newLatestSnapshotRoundId, service.Id);
                _context.ServiceStatsSnapshots.AddRange(snapshot.Values);
            }

            var latestServiceStates = await _context.RoundTeamServiceStates
                .TagWith("CalculateServiceStats:latestServiceStates")
                .Where(rtts => rtts.ServiceId == service.Id)
                .Where(rtts => rtts.GameRoundId == roundId)
                .AsNoTracking()
                .ToDictionaryAsync(rtss => rtss.TeamId);

            var serviceStates = await _context.RoundTeamServiceStates
                .TagWith("CalculateServiceStats:volatileServiceStates")
                .Where(rtts => rtts.ServiceId == service.Id)
                .Where(rtts => rtts.GameRoundId > newLatestSnapshotRoundId)
                .Where(rtts => rtts.GameRoundId <= roundId)
                .GroupBy(rtss => new { rtss.TeamId, rtss.Status })
                .Select(rtss => new { rtss.Key, Amount = rtss.Count() })
                .AsNoTracking()
                .ToDictionaryAsync(rtss => rtss.Key);

            // FlagServiceId, FlagRoundId, FlagOwnerId, FlagRoundOffset
            var lostFlags = (await _context.SubmittedFlags
                .TagWith("CalculateServiceStats:lostFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == service.Id)
                .Where(sf => sf.RoundId > newLatestSnapshotRoundId)
                .Where(sf => sf.RoundId <= roundId)
                .GroupBy(sf => new { sf.FlagServiceId, sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset })
                .Select(g => new { g.Key, Captures = g.Count() })           // Flag -> LossesOfFlag
                .ToArrayAsync())
                .GroupBy(f => f.Key.FlagOwnerId, f => f.Captures)           // This is not a SQL GROUP BY
                .ToDictionary(g => g.Key, g => g.AsEnumerable());           // TeamId -> [LossesOfFlag]

            var capturedFlags = (await _context.SubmittedFlags
                .TagWith("CalculateServiceStats:capturedFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == service.Id)
                .Where(sf => sf.RoundId > newLatestSnapshotRoundId)
                .Where(sf => sf.RoundId <= roundId)
                .ToArrayAsync())
                .GroupBy(sf => sf.AttackerTeamId)                           // This is not a SQL GROUP BY
                .ToDictionary(sf => sf.Key, sf => sf.AsEnumerable());

            var allCapturesOfFlags = await _context.SubmittedFlags
                .TagWith("CalculateServiceStats:allCapturesOfFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == service.Id)
                .Where(sf => sf.RoundId > newLatestSnapshotRoundId)
                .Where(sf => sf.RoundId <= roundId)
                .GroupBy(sf => new { sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset })
                .Select(g => new { g.Key, Amount = g.Count() })
                .ToDictionaryAsync(g => g.Key);

            var serviceStats = await _context.ServiceStats
                .Where(ss => ss.ServiceId == service.Id)
                .ToDictionaryAsync(ss => ss.TeamId);

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

                if (serviceStates.TryGetValue(new { TeamId = team.Id, Status = ServiceStatus.OK }, out var oks))
                {
                    slaPoints += oks.Amount * Math.Sqrt(teams.Length);
                }
                if (serviceStates.TryGetValue(new { TeamId = team.Id, Status = ServiceStatus.RECOVERING }, out var recoverings))
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

                serviceStats[team.Id].ServiceLevelAgreementPoints = slaPoints;
                serviceStats[team.Id].AttackPoints = attackPoints;
                serviceStats[team.Id].LostDefensePoints = defPoints;
                latestServiceStates.TryGetValue(team.Id, out var status_rtss);
                serviceStats[team.Id].Status = status_rtss?.Status ?? ServiceStatus.INTERNAL_ERROR;
                serviceStats[team.Id].ErrorMessage = status_rtss?.ErrorMessage;
            }
            await _context.SaveChangesAsync();
        }

        private async Task<Dictionary<long, ServiceStatsSnapshot>> CreateServiceSnapshot(Team[] teams, long newLatestSnapshotRoundId, long serviceId)
        {
            //TODO delete existing snapshot?
            var oldSnapshot = await GetSnapshot(teams, newLatestSnapshotRoundId - 1, serviceId);
            var lostFlags = (await _context.SubmittedFlags
                .TagWith("CreateSnapshot:lostFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == serviceId)
                .Where(sf => sf.RoundId == newLatestSnapshotRoundId)
                .GroupBy(sf => new { sf.FlagServiceId, sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset })
                .Select(g => new { g.Key, Captures = g.Count() })       // Flag -> LossesOfFlag
                .ToArrayAsync())
                .GroupBy(f => f.Key.FlagOwnerId, f => f.Captures)       // This is not a SQL GROUP BY
                .ToDictionary(g => g.Key, g => g.AsEnumerable());       // TeamId -> [LossesOfFlag]

            var capturedFlags = (await _context.SubmittedFlags
                .TagWith("CalculateServiceStats:capturedFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == serviceId)
                .Where(sf => sf.RoundId == newLatestSnapshotRoundId)
                .ToArrayAsync())
                .GroupBy(sf => sf.AttackerTeamId)                           // This is not a SQL GROUP BY
                .ToDictionary(sf => sf.Key, sf => sf.AsEnumerable());

            var serviceStates = await _context.RoundTeamServiceStates
                .TagWith("CreateSnapshot:serviceStates")
                .Where(rtts => rtts.ServiceId == serviceId)
                .Where(rtts => rtts.GameRoundId == newLatestSnapshotRoundId)
                .AsNoTracking()
                .ToDictionaryAsync(rtss => rtss.TeamId);

            var allCapturesOfFlags = await _context.SubmittedFlags
                .TagWith("CalculateServiceStats:allCapturesOfFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == serviceId)
                .Where(sf => sf.RoundId == newLatestSnapshotRoundId)
                .GroupBy(sf => new { sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset })
                .Select(g => new { g.Key, Amount = g.Count() })
                .ToDictionaryAsync(g => g.Key);

            var newServiceSnapshot = teams.ToDictionary(t => t.Id, t => new ServiceStatsSnapshot()
            {
                TeamId = t.Id,
                RoundId = newLatestSnapshotRoundId,
                ServiceId = serviceId
            });

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

                if (serviceStates.TryGetValue(team.Id, out var state))
                {
                    if (state.Status == ServiceStatus.OK)
                        slaPoints += 1.0 * Math.Sqrt(teams.Length);
                    if (state.Status == ServiceStatus.RECOVERING)
                        slaPoints += 0.5 * Math.Sqrt(teams.Length);
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
                var teamSnapshot = newServiceSnapshot[team.Id];
                teamSnapshot.ServiceLevelAgreementPoints = slaPoints;
                teamSnapshot.AttackPoints = attackPoints;
                teamSnapshot.LostDefensePoints = defPoints;
            }
            return newServiceSnapshot;
        }

        private async Task<Dictionary<long, ServiceStatsSnapshot>> GetSnapshot(Team[] teams, long snapshotRoundId, long serviceId)
        {
            var oldSnapshots = await _context.ServiceStatsSnapshots
                .TagWith("CalculateServiceScores:oldSnapshots")
                .Where(sss => sss.ServiceId == serviceId)
                .Where(sss => sss.RoundId == snapshotRoundId)
                .AsNoTracking()
                .ToDictionaryAsync(sss => sss.TeamId);

            if (oldSnapshots.Count == 0 && snapshotRoundId > 0)
            {
                return await CreateServiceSnapshot(teams, snapshotRoundId, serviceId);
            }
            return oldSnapshots; //TODO (re)create if not complete
        }

        public async Task<EnoEngineScoreboard> GetCurrentScoreboard(long roundId)
        {
            var sw = new Stopwatch();
            sw.Start();
            Logger.LogInformation($"{nameof(GetCurrentScoreboard)}Started Scoreboard Generation");
            var teams = _context.Teams
                .Include(t => t.ServiceStats)
                .AsNoTracking()
                .OrderByDescending(t => t.TotalPoints)
                .ToList();
            Logger.LogInformation($"{nameof(GetCurrentScoreboard)}Fetched teams after: {sw.ElapsedMilliseconds}ms");
            var round = _context.Rounds
                .AsNoTracking()
                .Where(r => r.Id == roundId)
                .FirstOrDefault();
            Logger.LogInformation($"{nameof(GetCurrentScoreboard)}Fetched round after: {sw.ElapsedMilliseconds}ms");
            var services = _context.Services
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .ToList();
            Logger.LogInformation($"{nameof(GetCurrentScoreboard)}Fetched services after: {sw.ElapsedMilliseconds}ms");
            Dictionary<(long serviceid, long flagindex), EnoScoreboardFirstblood> firstbloods = new Dictionary<(long serviceid, long flagindex), EnoScoreboardFirstblood>();
            foreach (var service in services)
            {
                for (int i = 0; i < service.FlagsPerRound; i++)
                {
                    var fb = await _context.SubmittedFlags
                        .Where(sf => sf.FlagServiceId == service.Id)
                        //.Where(sf => sf.FlagRoundOffset % service.FetchedFlagsPerRound == i)
                        .Where(sf => sf.FlagRoundOffset == i)
                        .OrderBy(sf => sf.Timestamp)
                        .FirstOrDefaultAsync();
                    if (fb != null)
                    {
                        var key = (fb.FlagServiceId, fb.FlagRoundOffset % service.FetchedFlagsPerRound);
                        var n = new EnoScoreboardFirstblood(DateTime.UtcNow, fb.AttackerTeamId, fb.RoundId, "StoreDescription", fb.FlagRoundOffset % service.FetchedFlagsPerRound);
                        if (!firstbloods.ContainsKey(key))
                        {
                            firstbloods[key] = n;
                        }
                        else if (firstbloods[key].FirstTime > n.FirstTime)
                        {
                            firstbloods[key] = n;
                        }
                    }
                }
            }
            Logger.LogInformation($"{nameof(GetCurrentScoreboard)}Iterated Firstblood after: {sw.ElapsedMilliseconds}ms");
            var scoreboard = new EnoEngineScoreboard(round, services, firstbloods, teams);
            Logger.LogInformation($"{nameof(GetCurrentScoreboard)}Finished after: {sw.ElapsedMilliseconds}ms");
            return scoreboard;
        }
    }
}
