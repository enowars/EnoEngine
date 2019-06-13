using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    class ServiceStatsSnapshot
    {
        public long Id { get; set; }
        public long TeamId { get; set; }
        public Team Team { get; set; }
        public long ServiceId { get; set; }
        public Service Service { get; set; }
        public double AttackPoints { get; set; }
        public double LostDefensePoints { get; set; }
        public double ServiceLevelAgreementPoints { get; set; }
        public ServiceStatus Status { get; set; }
        public long RoundId { get; set; }
        public Round Round { get; set; }
    }
}
