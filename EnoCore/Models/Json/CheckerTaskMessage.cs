using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public class CheckerTaskMessage
    {
        [JsonPropertyName("runId")]
        public long RunId { get; set; }
        [JsonPropertyName("method")]
        public string Method { get; set; } = default!;
        [JsonPropertyName("address")]
        public string Address { get; set; } = default!;
        [JsonPropertyName("serviceId")]
        public long ServiceId { get; set; }
        [JsonPropertyName("serviceName")]
        public string ServiceName { get; set; } = default!;
        [JsonPropertyName("teamId")]
        public long TeamId { get; set; }
        [JsonPropertyName("teamName")]
        public string TeamName { get; set; } = default!;
        [JsonPropertyName("relatedRoundId")]
        public long RelatedRoundId { get; set; }
        [JsonPropertyName("roundId")]
        public long Round { get; set; }
        [JsonPropertyName("flag")]
        public string? Flag { get; set; }
        [JsonPropertyName("flagIndex")]
        public long FlagIndex { get; set; }

        public CheckerTaskMessage() { }

        public CheckerTaskMessage(CheckerTask task)
        {
            RunId = task.Id;
            Method = task.TaskType;
            Address = task.Address;
            ServiceId = task.ServiceId;
            ServiceName = task.ServiceName;
            TeamId = task.TeamId;
            TeamName = task.TeamName;
            RelatedRoundId = task.RelatedRoundId;
            Round = task.CurrentRoundId;
            Flag = task.Payload;
            FlagIndex = task.TaskIndex;
        }
    }
}
