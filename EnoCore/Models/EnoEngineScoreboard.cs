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
        public string? StartTimestamp { get => Round?.Begin.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"); }
        public double? StartTimeEpoch { get => Round?.Begin.Subtract(DateTime.UnixEpoch).TotalSeconds; }
        public string? EndTimestamp { get => Round?.End.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"); }
        public double? EndTimeEpoch { get => Round?.End.Subtract(DateTime.UnixEpoch).TotalSeconds; }
        public EnoEngineScoreboardService[] Services { get; private set; }
        public EnoEngineScoreboardEntry[] Teams { get; private set; }

        public EnoEngineScoreboard(Round round, List<Service> services, Dictionary<(long serviceid, long flagindex), EnoScoreboardFirstblood> firstBloods, List<Team> teams)
        {
            Round = round;
            Services = services.Select(s => new EnoEngineScoreboardService(firstBloods
                    .Where(fbkv => fbkv.Key.serviceid == s.Id)
                    .Select(fbkv => fbkv.Value)
                    .ToArray(), s))
                .ToArray();
            Teams = teams.Select(t => new EnoEngineScoreboardEntry(t)).ToArray();
        }
    }
}
