using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public class EnoEngineScoreboardEntryServiceDetails
    {
        private readonly TeamServicePoints ServiceStats;

        public long ServiceId { get => ServiceStats.ServiceId; }
        public double AttackPoints { get => ServiceStats.AttackPoints; }
        public double LostDefensePoints { get => ServiceStats.LostDefensePoints; }
        public double ServiceLevelAgreementPoints { get => ServiceStats.ServiceLevelAgreementPoints; }
        [JsonPropertyName("ServiceStatus")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ServiceStatus ServiceStatus { get => ServiceStats.Status;  }
        public string? Message { get => ServiceStats.ErrorMessage; }

        public EnoEngineScoreboardEntryServiceDetails(TeamServicePoints serviceStats)
        {
            ServiceStats = serviceStats;
        }

        protected EnoEngineScoreboardEntryServiceDetails()
        {
            ServiceStats = default!;
        }
    }
}
