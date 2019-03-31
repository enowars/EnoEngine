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

    public class CheckerTask
    {
        public long Id { get; set; }
        public string TaskType { get; set; }
        public string Address { get; set; }
        public long ServiceId { get; set; }
        public string ServiceName { get; set; }
        public long TeamId { get; set; }
        public string TeamName { get; set; }
        public long RelatedRoundId { get; set; }
        public long CurrentRoundId { get; set; }
        public string Payload { get; set; }
        public DateTime StartTime { get; set; }
        public long MaxRunningTime { get; set; }
        public long TaskIndex { get; set; }
        public int CheckerResult { get; set; }
        public CheckerTaskLaunchStatus CheckerTaskLaunchStatus { get; set; }

        public CheckerTask()
        {

        }
    }
}
