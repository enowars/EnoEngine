using EnoCore.Models.Database;
using EnoCore.Models.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace EnoCore.Logging
{
    public class EnoStatistics
    {
        private static readonly string PREFIX = "##ENOSTATISTICSMESSAGE ";
        private readonly FileQueue Queue;

        public EnoStatistics(string tool)
        {
            Queue = new FileQueue($"../data/{tool}.statistics.log", CancellationToken.None);
        }

        public void SubmissionBatchMessage(long flagsProcessed, long okFlags,
            long duplicateFlags, long oldFlags, long duration)
        {
            var message = new SubmissionBatchMessage(flagsProcessed,
                okFlags, duplicateFlags, oldFlags, duration);
            Queue.Enqueue(PREFIX + JsonSerializer.Serialize(message) + "\n");
        }

        public void CheckerTaskLaunchMessage(CheckerTask task)
        {
            var message = new CheckerTaskLaunchMessage(task);
            Queue.Enqueue(PREFIX + JsonSerializer.Serialize(message) + "\n");
        }
        public void CheckerTaskFinishedMessage(long roundId, string message, long time)
        {
            var msg = new CheckerTaskAggregateMessage(roundId, message, time);
            Queue.Enqueue(PREFIX + JsonSerializer.Serialize(msg) + "\n");
        }
    }

    public class EnoStatisticsMessage
    {
        public string MessageType => GetType().Name;
        public string Timestamp { get; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    public class SubmissionBatchMessage : EnoStatisticsMessage
    {
        public long FlagsProcessed { get; }
        public long OkFlags { get; }
        public long DuplicateFlags { get; }
        public long OldFlags { get; }
        public long Duration { get; }

        public SubmissionBatchMessage(long flagsProcessed, long okFlags,
            long duplicateFlags, long oldFlags, long duration)
        {
            FlagsProcessed = flagsProcessed;
            OkFlags = okFlags;
            DuplicateFlags = duplicateFlags;
            OldFlags = oldFlags;
            Duration = duration;
        }
    }

    public class CheckerTaskLaunchMessage : EnoStatisticsMessage
    {
        public long RoundId { get; }
        public string ServiceName { get; }
        public string Method { get; }
        public long TaskIndex { get; }

        public CheckerTaskLaunchMessage(CheckerTask task)
        {
            RoundId = task.CurrentRoundId;
            ServiceName = task.ServiceName;
            Method = task.Method.ToString();
            TaskIndex = task.TaskIndex;
        }
    }
    public class CheckerTaskFinishedMessage : EnoStatisticsMessage
    {
        public long RoundId { get; }
        public string ServiceName { get; }
        public string Method { get; }
        public long TaskIndex { get; }

        public CheckerTaskFinishedMessage(CheckerTask task)
        {
            RoundId = task.CurrentRoundId;
            ServiceName = task.ServiceName;
            Method = task.Method.ToString();
            TaskIndex = task.TaskIndex;
        }
    }
    public class CheckerTaskAggregateMessage : EnoStatisticsMessage
    {
        public long RoundId { get; }
        public long Time { get; }
        public string Message { get; }

        public CheckerTaskAggregateMessage(long roundId, string message, long time)
        {
            RoundId = roundId;
            Message = message;
            Time = time;
        }
    }
}
