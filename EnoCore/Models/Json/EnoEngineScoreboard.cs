using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnoCore.Models.Json
{
    public class EnoEngineScoreboard
    {
        public long? CurrentRound { get; set; }
        public string? StartTimestamp { get; set; }
        public double? StartTimeEpoch { get; set; }
        public string? EndTimestamp { get; set; }
        public double? EndTimeEpoch { get; set; }
        public EnoEngineScoreboardService[] Services { get; set; }
        public EnoEngineScoreboardEntry[] Teams { get; set; }
#pragma warning disable CS8618
        public EnoEngineScoreboard() { }
#pragma warning restore CS8618
        public EnoEngineScoreboard(Round? round, List<Service> services, Dictionary<(long serviceid, long flagindex), EnoScoreboardFirstblood> firstBloods, List<Team> teams)
        {
            CurrentRound = round?.Id;
            StartTimestamp = round?.Begin.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            StartTimeEpoch = round?.Begin.Subtract(DateTime.UnixEpoch).TotalSeconds;
            EndTimestamp = round?.End.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            EndTimeEpoch = round?.End.Subtract(DateTime.UnixEpoch).TotalSeconds;
            Services = services.Select(s => new EnoEngineScoreboardService(firstBloods
                    .Where(fbkv => fbkv.Key.serviceid == s.Id)
                    .Select(fbkv => fbkv.Value)
                    .ToArray(), s))
                .ToArray();
            Teams = teams.Select(t => new EnoEngineScoreboardEntry(t)).ToArray();
        }
    }
}
