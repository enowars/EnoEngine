namespace EnoCore.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using EnoCore.Models.Database;
    using EnoCore.Models.Json;

    public sealed class EnoStatistics : IDisposable
    {
        private static readonly string PREFIX = "##ENOSTATISTICSMESSAGE ";
        private readonly FileQueue queue;

        public EnoStatistics(string tool)
        {
            this.queue = new FileQueue($"../data/{tool}.statistics.log", CancellationToken.None);
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

#pragma warning disable SA1201 // Elements should appear in the correct order
    public record EnoStatisticsMessage(
#pragma warning restore SA1201 // Elements should appear in the correct order
        string MessageType,
        string Timestamp);

    public record SubmissionBatchMessage(
        long FlagsProcessed,
        long OkFlags,
        long DuplicateFlags,
        long OldFlags,
        long Duration)
        : EnoStatisticsMessage(nameof(SubmissionBatchMessage), EnoCoreUtil.GetCurrentTimestamp());

    public record CheckerTaskLaunchMessage(
        long RoundId,
        string ServiceName,
        string Method,
        long TaskIndex)
        : EnoStatisticsMessage(nameof(CheckerTaskLaunchMessage), EnoCoreUtil.GetCurrentTimestamp())
    {
        public static CheckerTaskLaunchMessage FromCheckerTask(CheckerTask task)
        {
            return new(
                task.CurrentRoundId,
                task.ServiceName,
                task.Method.ToString(),
                task.TaskIndex);
        }
    }

    public record CheckerTaskFinishedMessage(
        long RoundId,
        string ServiceName,
        string Metho,
        long TaskIndex,
        double Duration,
        string Result)
        : EnoStatisticsMessage(nameof(CheckerTaskFinishedMessage), EnoCoreUtil.GetCurrentTimestamp())
    {
        public static CheckerTaskFinishedMessage FromCheckerTask(CheckerTask task)
        {
            return new(
                task.CurrentRoundId,
                task.ServiceName,
                task.Method.ToString(),
                task.TaskIndex,
                (DateTime.UtcNow - task.StartTime).TotalSeconds,
                task.CheckerResult.ToString());
        }
    }

    public record CheckerTaskAggregateMessage(
        long RoundId,
        long Time)
        : EnoStatisticsMessage(nameof(CheckerTaskAggregateMessage), EnoCoreUtil.GetCurrentTimestamp());

    public record TeamFlagSubmissionStatisticMessage(
        string TeamName,
        long TeamId,
        long OkFlags,
        long DuplicateFlags,
        long OldFlags,
        long InvalidFlags,
        long OwnFlags)
        : EnoStatisticsMessage(nameof(TeamFlagSubmissionStatisticMessage), EnoCoreUtil.GetCurrentTimestamp());
}
