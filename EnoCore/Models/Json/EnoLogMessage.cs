using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace EnoCore.Models.Json
{
    public class EnoLogMessage
    {
        public string? Tool { get; set; }
        public string? Type { get; set; } = "infrastructure";
        public string? Severity { get; set; }
        public string? Timestamp { get; set; }
        public string? Module { get; set; }
        public string? Function { get; set; }
        public string? Flag { get; set; }
        public long? FlagIndex { get; set; }
        public long? CheckerTaskId { get; set; }
        public long? RoundId { get; set; }
        public long? RelatedRoundId { get; set; }
        public string? Message { get; set; }
        public string? TeamName { get; set; }
        public string? ServiceName { get; set; }
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
