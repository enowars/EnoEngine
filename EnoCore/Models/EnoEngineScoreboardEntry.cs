using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnoCore.Models
{
    public class EnoEngineScoreboardEntry
    {
        private readonly Team Team;

        public string Name { get => Team.Name; }
        public long TeamId { get => Team.Id; }
        public double TotalPoints { get => Team.TotalPoints; }
        public double AttackPoints { get => Team.AttackPoints; }
        public double LostDefensePoints { get => Team.LostDefensePoints; }
        public double ServiceLevelAgreementPoints { get => Team.ServiceLevelAgreementPoints; }
        public Dictionary<long, EnoEngineScoreboardEntryServiceDetails> ServiceDetails { get; set; }


        public EnoEngineScoreboardEntry(Team team, IEnumerable<ServiceStats> serviceStats)
        {
            Team = team;
            ServiceDetails = new Dictionary<long, EnoEngineScoreboardEntryServiceDetails>();
            foreach (var service in serviceStats)
            {
                ServiceDetails.Add(service.ServiceId, new EnoEngineScoreboardEntryServiceDetails(service));
            }
        }
    }
}
