namespace EnoDatabase
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.AttackInfo;
    using EnoCore.Configuration;
    using EnoCore.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    public partial class EnoDatabase : IEnoDatabase
    {
        public async Task<AttackInfo> GetAttackInfo(long roundId, Configuration config)
        {
            var teamAddresses = await this.context.Teams
                .AsNoTracking()
                .Select(t => new { t.Id, t.Address })
                .ToDictionaryAsync(t => t.Id, t => t.Address);
            var availableTeams = await this.context.RoundTeamServiceStatus
                .Where(rtss => rtss.GameRoundId == roundId)
                .GroupBy(rtss => rtss.TeamId)
                .Select(g => new { g.Key, BestResult = g.Min(rtss => rtss.Status) })
                .Where(ts => ts.BestResult < ServiceStatus.OFFLINE)
                .Select(ts => ts.Key)
                .OrderBy(ts => ts)
                .ToArrayAsync();
            var availableTeamAddresses = availableTeams.Select(id => teamAddresses[id] ?? id.ToString()).ToArray();

            var serviceNames = await this.context.Services
                .AsNoTracking()
                .Select(s => new { s.Id, s.Name })
                .ToDictionaryAsync(s => s.Id, s => s.Name);

            var relevantTasks = await this.context.CheckerTasks
                .AsNoTracking()
                .Where(ct => ct.CurrentRoundId > roundId - config.FlagValidityInRounds)
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
    }
}
