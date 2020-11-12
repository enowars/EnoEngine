namespace EnoCore.Models.Database
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using EnoCore.Models.Json;

    public enum CheckerTaskMethod
    {
#pragma warning disable SA1300 // Element should begin with upper-case letter
        putflag,
        getflag,
        putnoise,
        getnoise,
        havoc,
#pragma warning restore SA1300 // Element should begin with upper-case letter
    }

    public enum CheckerResult
    {
        INTERNAL_ERROR,
        OFFLINE,
        MUMBLE,
        OK,
    }

    public enum CheckerTaskLaunchStatus
    {
        New,
        Launched,
        Done,
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    public sealed record CheckerTask(
#pragma warning restore SA1201 // Elements should appear in the correct order
        long Id,
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
