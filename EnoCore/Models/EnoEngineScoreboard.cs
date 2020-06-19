using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnoCore.Models
{
    public class EnoEngineScoreboard
    {
        private readonly Round Round;

        public long? CurrentRound { get => Round?.Id; }
        public string? StartTimestamp { get => Round?.Begin.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"); }
        public double? StartEpoch { get => Round?.Begin.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds; }
        public string? EndTimestamp { get => Round?.End.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"); }
        public double? EndEpoch { get => Round?.End.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds; }
        public EnoEngineScoreboardService[] Services { get; private set; }
        public EnoEngineScoreboardEntry[] Teams { get; private set; }

        public EnoEngineScoreboard(Round round, List<Service> services, Dictionary<(long serviceid, long flagindex), EnoScoreboardFirstblood> firstbloods, List<Team> teams)
        {
            Round = round;
            Services = services.Select(s => new EnoEngineScoreboardService(firstbloods
                    .Where(fbkv => fbkv.Key.serviceid == s.Id)
                    .Select(fbkv => fbkv.Value)
                    .ToArray(), s))
                .ToArray();
            Teams = teams.Select(t => new EnoEngineScoreboardEntry(t)).ToArray();
        }
    }
}
