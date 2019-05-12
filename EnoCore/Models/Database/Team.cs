using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models
{
    public class Team
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string VulnboxAddress { get; set; }
        public string GatewayAddress { get; set; }
        public double TotalPoints { get; set; }
        public double AttackPoints { get; set; }
        public double LostDefensePoints { get; set; }
        public double ServiceLevelAgreementPoints { get; set; }
        public List<RoundTeamServiceState> ServiceDetails { get; set; }
        public long ServiceStatsId { get; set; }
        public List<ServiceStats> ServiceStats { get; set; }
    }
}
