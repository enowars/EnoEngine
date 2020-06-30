using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public class EnoLogMessage
    {
        [JsonPropertyName("tool")]
        public string? Tool { get; set; }
        [JsonPropertyName("type")]
        public string? Type { get; set; } = "infrastructure";
        [JsonPropertyName("severity")]
        public string Severity { get; set; } = default!;
        [JsonPropertyName("severityLevel")]
        public long SeverityLevel { get; set; }
        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }
        [JsonPropertyName("module")]
        public string? Module { get; set; }
        [JsonPropertyName("function")]
        public string? Function { get; set; }
        [JsonPropertyName("flag")]
        public string? Flag { get; set; }
        [JsonPropertyName("flagIndex")]
        public long? FlagIndex { get; set; }
        [JsonPropertyName("runId")]
        public long? RunId { get; set; }
        [JsonPropertyName("roundId")]
        public long? RoundId { get; set; }
        [JsonPropertyName("relatedRoundId")]
        public long? RelatedRoundId { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; } = default!;
        [JsonPropertyName("teamName")]
        public string? TeamName { get; set; }
        [JsonPropertyName("teamId")]
        public long TeamId { get; set; }
        [JsonPropertyName("serviceName")]
        public string? ServiceName { get; set; }
        [JsonPropertyName("method")]
        public string? Method { get; set; }

        public void FromCheckerTask(CheckerTask task)
        {
            Flag = task.Payload;
            RoundId = task.CurrentRoundId;
            RelatedRoundId = task.RelatedRoundId;
            TeamName = task.TeamName;
            TeamId = task.TeamId;
            RunId = task.Id;
            FlagIndex = task.TaskIndex;
            ServiceName = task.ServiceName;
            Method = task.Method.ToString();
        }

        public void FromCheckerTaskMessage(CheckerTaskMessage taskMessage)
        {
            Flag = taskMessage.Flag;
            RoundId = taskMessage.RoundId;
            RelatedRoundId = taskMessage.RelatedRoundId;
            TeamName = taskMessage.TeamName;
            TeamId = taskMessage.TeamId;
            RunId = taskMessage.RunId;
            FlagIndex = taskMessage.FlagIndex;
            ServiceName = taskMessage.ServiceName;
            Method = taskMessage.Method.ToString();
        }
    }
}
