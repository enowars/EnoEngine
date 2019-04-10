using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace EnoCore.Models.Database
{
    public enum CheckerTaskLaunchStatus
    {
        New,
        Launched,
        Done
    }

    public enum CheckerResult
    {
        CheckerError,
        Ok,
        Mumble,
        Down
    }

    public class CheckerTask
    {
        [JsonProperty("runId")]
        public long Id { get; set; }
        [JsonProperty("method")]
        public string TaskType { get; set; }
        [JsonProperty("address")]
        public string Address { get; set; }
        [JsonProperty("serviceId")]
        public long ServiceId { get; set; }
        [JsonProperty("serviceName")]
        public string ServiceName { get; set; }
        [JsonProperty("teamId")]
        public long TeamId { get; set; }
        [JsonProperty("team")]
        public string TeamName { get; set; }
        [JsonProperty("relatedRoundId")]
        public long RelatedRoundId { get; set; }
        [JsonProperty("round")]
        public long CurrentRoundId { get; set; }
        [JsonProperty("flag")]
        public string Payload { get; set; }
        [JsonIgnore]
        public DateTime StartTime { get; set; }
        [JsonProperty("timeout")]
        public int MaxRunningTime { get; set; }
        [JsonProperty("flagIndex")]
        public long TaskIndex { get; set; }
        [JsonIgnore]
        public CheckerResult CheckerResult { get; set; }
        [JsonIgnore]
        public CheckerTaskLaunchStatus CheckerTaskLaunchStatus { get; set; }
        [NotMapped]
        [JsonProperty("loggingEndpoint")]
        public string EnoLogsDomain { get; set; } = "http://172.17.0.1:8080/api/insertLogs";

        public CheckerTask()
        {

        }
    }
}
