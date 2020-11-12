namespace EnoCore.Models.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using EnoCore.Models.Database;

    public class EnoEngineScoreboard
    {
#pragma warning disable CS8618
        public EnoEngineScoreboard()
        {
        }

#pragma warning restore CS8618
        public EnoEngineScoreboard(Round? round, List<Service> services, Dictionary<(long ServiceId, long FlagIndex), EnoScoreboardFirstblood> firstBloods, List<Team> teams)
        {
            this.CurrentRound = round?.Id;
            this.StartTimestamp = round?.Begin.ToString(EnoCoreUtil.DateTimeFormat);
            this.StartTimeEpoch = round?.Begin.Subtract(DateTime.UnixEpoch).TotalSeconds;
            this.EndTimestamp = round?.End.ToString(EnoCoreUtil.DateTimeFormat);
            this.EndTimeEpoch = round?.End.Subtract(DateTime.UnixEpoch).TotalSeconds;
            this.Services = services.Select(s => new EnoEngineScoreboardService(
                s.Id,
                s.Name,
                s.FlagStores,
                firstBloods
                    .Where(fbkv => fbkv.Key.ServiceId == s.Id)
                    .Select(fbkv => fbkv.Value)
                    .ToArray()))
                .ToArray();
            this.Teams = teams.Select(t => new EnoEngineScoreboardEntry(t)).ToArray();
        }

        public long? CurrentRound { get; set; }
        public string? StartTimestamp { get; set; }
        public double? StartTimeEpoch { get; set; }
        public string? EndTimestamp { get; set; }
        public double? EndTimeEpoch { get; set; }
        public EnoEngineScoreboardService[] Services { get; set; }
        public EnoEngineScoreboardEntry[] Teams { get; set; }
    }
}
