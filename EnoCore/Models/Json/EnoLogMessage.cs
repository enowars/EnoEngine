using EnoCore.Models.Database;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class EnoLogMessage
    {
        [JsonProperty("tool")]
        public string Tool { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; } = "infrastructure";
        [JsonProperty("severity")]
        public string Severity { get; set; }
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
        [JsonProperty("module")]
        public string Module { get; set; }
        [JsonProperty("function")]
        public string Function { get; set; }
        [JsonProperty("flag")]
        public string Flag { get; set; }
        [JsonProperty("flagIndex")]
        public long? FlagIndex { get; set; }
        [JsonProperty("runId")]
        public long? CheckerTaskId { get; set; }
        [JsonProperty("round")]
        public long? RoundId { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("teamName")]
        public string TeamName { get; set; }
        [JsonProperty("serviceName")]
        public string ServiceName { get; set; }


        public static EnoLogMessage FromCheckerTask(CheckerTask task)
        {
            return new EnoLogMessage()
            {
                Flag = task.Payload,
                RoundId = task.CurrentRoundId,
                TeamName = task.TeamName,
                Timestamp = DateTime.UtcNow.ToLongTimeString(),
                CheckerTaskId = task.Id,
                FlagIndex = task.TaskIndex,
                ServiceName = task.ServiceName
            };
        }
    }
}
