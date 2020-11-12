namespace EnoCore.Models.Json
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;
    using EnoCore.Models.Database;

    public class EnoEngineScoreboardEntryServiceDetails
    {
        private readonly TeamServicePoints serviceStats;

        public EnoEngineScoreboardEntryServiceDetails(TeamServicePoints serviceStats)
        {
            this.serviceStats = serviceStats;
        }

        protected EnoEngineScoreboardEntryServiceDetails()
        {
            this.serviceStats = default!;
        }

        public long ServiceId { get => this.serviceStats.ServiceId; }
        public double AttackPoints { get => this.serviceStats.AttackPoints; }
        public double LostDefensePoints { get => this.serviceStats.DefensePoints; }
        public double ServiceLevelAgreementPoints { get => this.serviceStats.ServiceLevelAgreementPoints; }
        [JsonPropertyName("ServiceStatus")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ServiceStatus ServiceStatus { get => this.serviceStats.Status; }
        public string? Message { get => this.serviceStats.ErrorMessage; }
    }
}
