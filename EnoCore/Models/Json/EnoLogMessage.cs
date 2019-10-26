using EnoCore.Models.Database;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace EnoCore.Models.Json
{
    public class EnoLogMessage
    {
        [JsonProperty("tool")]
        public string? Tool { get; set; }
        [JsonProperty("type")]
        public string? Type { get; set; } = "infrastructure";
        [JsonProperty("severity")]
        public string? Severity { get; set; }
        [JsonProperty("timestamp")]
        public string? Timestamp { get; set; }
        [JsonProperty("module")]
        public string? Module { get; set; }
        [JsonProperty("function")]
        public string? Function { get; set; }
        [JsonProperty("flag")]
        public string? Flag { get; set; }
        [JsonProperty("flagIndex")]
        public long? FlagIndex { get; set; }
        [JsonProperty("runId")]
        public long? CheckerTaskId { get; set; }
        [JsonProperty("round")]
        public long? RoundId { get; set; }
        [JsonProperty("relatedRound")]
        public long? RelatedRoundId { get; set; }
        [JsonProperty("message")]
        public string? Message { get; set; }
        [JsonProperty("teamName")]
        public string? TeamName { get; set; }
        [JsonProperty("serviceName")]
        public string? ServiceName { get; set; }
        [JsonProperty("method")]
        public string? Method { get; set; }


        public void FromCheckerTask(CheckerTask task)
        {
            Flag = task.Payload;
            RoundId = task.CurrentRoundId;
            RelatedRoundId = task.RelatedRoundId;
            TeamName = task.TeamName;
            CheckerTaskId = task.Id;
            FlagIndex = task.TaskIndex;
            ServiceName = task.ServiceName;
            Method = task.TaskType;
        }

        public static EnoLogMessage FromRound(Round round)
        {
            return new EnoLogMessage()
            {
                RoundId = round.Id
            };
        }
    }
}
