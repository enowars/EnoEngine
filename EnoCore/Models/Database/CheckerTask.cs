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

    public record CheckerTask(long Id,
        string CheckerUrl,
        CheckerTaskMethod Method,
        string Address,
        long ServiceId,
        string ServiceName,
        long TeamId,
        string TeamName,
        long RelatedRoundId,
        long CurrentRoundId,
        string? Payload,
        DateTime StartTime,
        int MaxRunningTime,
        long RoundLength,
        long TaskIndex,
        CheckerResult CheckerResult,
        string? ErrorMessage,
        CheckerTaskLaunchStatus CheckerTaskLaunchStatus);
}
