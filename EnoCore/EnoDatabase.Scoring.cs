using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnoCore
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
            var stats = new CalculateServiceStatsMessage();
            Dictionary<long, ServiceStatsSnapshot> snapshot = null;
            if (newLatestSnapshotRoundId > 0)
            {
                snapshot = await CreateServiceSnapshot(teams, newLatestSnapshotRoundId, service.Id);
                _context.ServiceStatsSnapshots.AddRange(snapshot.Values);
            }

            var lostFlags = (await _context.Flags
                .TagWith("CreateSnapshot:lostFlags")
                .AsNoTracking()
                .Where(f => f.ServiceId == service.Id)
                .Where(f => f.RoundId >= newLatestSnapshotRoundId)
                .Where(f => f.RoundId <= roundId)
                .Where(f => f.Captures > 0)
                .ToArrayAsync())
                .GroupBy(f => f.OwnerId, f => f.Captures)
                .ToDictionary(g => g.Key, sf => sf.AsEnumerable());
            var capturedFlags = (await _context.SubmittedFlags
                .TagWith("CreateSnapshot:capturedFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == service.Id)
                .Where(sf => sf.FlagRoundId >= newLatestSnapshotRoundId)
                .Where(sf => sf.FlagRoundId <= roundId)
                .Include(sf => sf.Flag)
                .ToArrayAsync())
                .GroupBy(sf => sf.AttackerTeamId)
                .ToDictionary(sf => sf.Key, sf => sf.AsEnumerable());

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
                        attackPoints += 1.0 / capture.Flag.Captures;
                    }
                }

                serviceStats[team.Id].ServiceLevelAgreementPoints = slaPoints;
                serviceStats[team.Id].AttackPoints = attackPoints;
                serviceStats[team.Id].LostDefensePoints = defPoints;
            }
            await _context.SaveChangesAsync();
        }

        private async Task<Dictionary<long, ServiceStatsSnapshot>> CreateServiceSnapshot(Team[] teams, long newLatestSnapshotRoundId, long serviceId)
        {
            var oldSnapshot = await GetSnapshot(teams, newLatestSnapshotRoundId - 1, serviceId);
            var lostFlags = (await _context.Flags
                .TagWith("CreateSnapshot:lostFlags")
                .AsNoTracking()
                .Where(f => f.ServiceId == serviceId)
                .Where(f => f.RoundId == newLatestSnapshotRoundId)
                .Where(f => f.Captures > 0)
                .ToArrayAsync())
                .GroupBy(f => f.OwnerId, f => f.Captures)
                .ToDictionary(g => g.Key, sf => sf.AsEnumerable());
            var capturedFlags = (await _context.SubmittedFlags
                .TagWith("CreateSnapshot:capturedFlags")
                .AsNoTracking()
                .Where(sf => sf.FlagServiceId == serviceId)
                .Where(sf => sf.FlagRoundId == newLatestSnapshotRoundId)
                .Include(sf => sf.Flag)
                .ToArrayAsync())
                .GroupBy(sf => sf.AttackerTeamId)
                .ToDictionary(sf => sf.Key, sf => sf.AsEnumerable());

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
                        attackPoints += 1.0 / capture.Flag.Captures;
                    }
                }
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

        public EnoEngineScoreboard GetCurrentScoreboard(long roundId)
        {
            var teams = _context.Teams.AsNoTracking().OrderByDescending(t => t.TotalPoints).ToList();
            var round = _context.Rounds
                .AsNoTracking()
                .Where(r => r.Id == roundId)
                .FirstOrDefault();
            var services = _context.Services.AsNoTracking().ToList();
            var scoreboard = new EnoEngineScoreboard(round, services);
            foreach (var team in teams)
            {
                var details = _context.ServiceStats
                    .Where(ss => ss.TeamId == team.Id)
                    .AsNoTracking()
                    .ToList();
                scoreboard.Teams.Add(new EnoEngineScoreboardEntry(team, details));
            }
            return scoreboard;
        }
    }
}
