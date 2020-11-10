using EnoCore.Models.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    public enum CheckerTaskMethod
    {
        putflag,
        getflag,
        putnoise,
        getnoise,
        havoc
    }

    public enum CheckerResult
    {
        INTERNAL_ERROR,
        OFFLINE,
        MUMBLE,
        OK
    }

    public enum CheckerTaskLaunchStatus
    {
        New,
        Launched,
        Done
    }

    public sealed record CheckerTask(long Id,
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
