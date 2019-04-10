using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    public enum LogSeverity
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public class CheckerLogMessage
    {
        [JsonIgnore]
        public long Id { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
        [JsonProperty("severity")]
        public LogSeverity Severity { get; set; }
        [JsonIgnore]
        public CheckerTask RelatedTask { get; set; }
        [JsonProperty("runId")]
        public long RelatedTaskId { get; set; }
        [JsonProperty("tag")]
        public string Origin { get; set; }
    }
}
