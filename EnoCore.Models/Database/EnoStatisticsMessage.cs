﻿namespace EnoCore.Models.Database;

public record EnoStatisticsMessage(
    string MessageType,
    string Timestamp);

public record SubmissionBatchMessage(
    long FlagsProcessed,
    long OkFlags,
    long DuplicateFlags,
    long OldFlags,
    long Duration)
    : EnoStatisticsMessage(nameof(SubmissionBatchMessage), DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

public record CheckerTaskLaunchMessage(
    long RoundId,
    string ServiceName,
    long TeamId,
    string Method,
    long TaskIndex)
    : EnoStatisticsMessage(nameof(CheckerTaskLaunchMessage), DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
{
    public static CheckerTaskLaunchMessage FromCheckerTask(CheckerTask task)
    {
        return new(
            task.CurrentRoundId,
            task.ServiceName,
            task.TeamId,
            task.Method.ToString(),
            task.UniqueVariantId);
    }
}

public record CheckerTaskFinishedMessage(
    long RoundId,
    string ServiceName,
    long TeamId,
    string Method,
    long TaskIndex,
    double Duration,
    string Result)
    : EnoStatisticsMessage(nameof(CheckerTaskFinishedMessage), DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
{
    public static CheckerTaskFinishedMessage FromCheckerTask(CheckerTask task)
    {
        return new(
            task.CurrentRoundId,
            task.ServiceName,
            task.TeamId,
            task.Method.ToString(),
            task.UniqueVariantId,
            (DateTime.UtcNow - task.StartTime).TotalSeconds,
            task.CheckerResult.ToString());
    }
}

public record CheckerTaskAggregateMessage(
    long RoundId,
    long Time)
    : EnoStatisticsMessage(nameof(CheckerTaskAggregateMessage), DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

public record TeamFlagSubmissionStatisticMessage(
    string TeamName,
    long TeamId,
    long OkFlags,
    long DuplicateFlags,
    long OldFlags,
    long InvalidFlags,
    long OwnFlags)
    : EnoStatisticsMessage(nameof(TeamFlagSubmissionStatisticMessage), DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
