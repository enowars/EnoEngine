using EnoCore.Models.Json;
using System;
using System.Collections.Generic;
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
        INTERNAL_ERROR,
        OFFLINE,
        MUMBLE,
        OK
    }

    public class CheckerTask
    {
#pragma warning disable CS8618
        public long Id { get; set; }
        public string CheckerUrl { get; set; }
        public CheckerTaskMethod Method { get; set; }
        public string Address { get; set; }
        public long ServiceId { get; set; }
        public string ServiceName { get; set; }
        public long TeamId { get; set; }
        public string TeamName { get; set; }
        public long RelatedRoundId { get; set; }
        public long CurrentRoundId { get; set; }
        public string? Payload { get; set; }
        public DateTime StartTime { get; set; }
        public int MaxRunningTime { get; set; }
        public long RoundLength { get; set; }
        public long TaskIndex { get; set; }
        public CheckerResult CheckerResult { get; set; }
        public string? ErrorMessage { get; set; }
        public CheckerTaskLaunchStatus CheckerTaskLaunchStatus { get; set; }
#pragma warning restore CS8618
    }
}
