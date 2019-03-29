using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models
{
    public class EnoEngineScoreboardEntryServiceDetails
    {
        private readonly ServiceStats ServiceStats;

        public long ServiceId { get => ServiceStats.ServiceId; }
        public double AttackPoints { get => ServiceStats.AttackPoints; }
        public double LostDefensePoints { get => ServiceStats.LostDefensePoints; }
        public double ServiceLevelAgreementPoints { get => ServiceStats.ServiceLevelAgreementPoints; }
        public ServiceStatus ServiceStatus { get => ServiceStats.Status;  }

        public EnoEngineScoreboardEntryServiceDetails(ServiceStats serviceStats)
        {
            ServiceStats = serviceStats;
        }
    }
}
