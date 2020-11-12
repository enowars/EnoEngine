namespace EnoCore.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using EnoCore.Models;

    public sealed class EnoStatistics : IDisposable
    {
        private static readonly string PREFIX = "##ENOSTATISTICSMESSAGE ";
        private readonly FileQueue queue;

        public EnoStatistics(string tool)
        {
            this.queue = new FileQueue($"{EnoCoreUtil.DataDirectory}{tool}.statistics.log", CancellationToken.None);
        }

        public void LogSubmissionBatchMessage(
            long flagsProcessed,
            long okFlags,
            long duplicateFlags,
            long oldFlags,
            long duration)
        {
            var message = new SubmissionBatchMessage(
                flagsProcessed,
                okFlags,
                duplicateFlags,
                oldFlags,
                duration);
            this.queue.Enqueue(PREFIX + JsonSerializer.Serialize(message) + "\n");
        }

        public void LogCheckerTaskLaunchMessage(CheckerTask task)
        {
            var message = CheckerTaskLaunchMessage.FromCheckerTask(task);
            this.queue.Enqueue(PREFIX + JsonSerializer.Serialize(message) + "\n");
        }

        public void LogCheckerTaskAggregateMessage(long roundId, long time)
        {
            var msg = new CheckerTaskAggregateMessage(roundId, time);
            this.queue.Enqueue(PREFIX + JsonSerializer.Serialize(msg) + "\n");
        }

        public void LogCheckerTaskFinishedMessage(CheckerTask task)
        {
            var msg = CheckerTaskFinishedMessage.FromCheckerTask(task);
            this.queue.Enqueue(PREFIX + JsonSerializer.Serialize(msg) + "\n");
        }

        public void FlagSubmissionStatisticsMessage(string teamName, long teamId, long okFlags, long duplicateFlags, long oldFlags, long invalidFlags, long ownFlags)
        {
            var msg = new TeamFlagSubmissionStatisticMessage(teamName, teamId, okFlags, duplicateFlags, oldFlags, invalidFlags, ownFlags);
            this.queue.Enqueue(PREFIX + JsonSerializer.Serialize(msg) + "\n");
        }

        public void Dispose()
        {
            this.queue.Dispose();
        }
    }
}
