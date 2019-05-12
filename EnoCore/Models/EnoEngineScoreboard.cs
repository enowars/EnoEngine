using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models
{
    public class EnoEngineScoreboard
    {
        private readonly Round Round;

        public long CurrentRound { get => Round?.Id ?? 0; }
        public List<EnoEngineScoreboardEntry> Teams { get; set; } = new List<EnoEngineScoreboardEntry>();
        public Dictionary<long, EnoEngineScoreboardService> Services { get; } = new Dictionary<long, EnoEngineScoreboardService>();

        public EnoEngineScoreboard(Round round, List<Service> services)
        {
            Round = round;
            foreach (var service in services)
            {
                Services[service.Id] = new EnoEngineScoreboardService()
                {
                    Name = service.Name
                };
            }
        }
    }
}
