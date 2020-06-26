using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public class EnoEngineScoreboardEntryServiceDetails
    {
        private readonly ServiceStats ServiceStats;

        public long ServiceId { get => ServiceStats.ServiceId; }
        public double AttackPoints { get => ServiceStats.AttackPoints; }
        public double LostDefensePoints { get => ServiceStats.LostDefensePoints; }
        public double ServiceLevelAgreementPoints { get => ServiceStats.ServiceLevelAgreementPoints; }
        [JsonPropertyName("ServiceStatus")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ServiceStatus ServiceStatus { get => ServiceStats.Status;  }
        public string Message { get => "No checker details available"; }

        public EnoEngineScoreboardEntryServiceDetails(ServiceStats serviceStats)
        {
            ServiceStats = serviceStats;
        }
    }
}
