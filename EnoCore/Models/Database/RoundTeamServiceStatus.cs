using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    public enum ServiceStatus
    {
        INTERNAL_ERROR,
        OK,
        RECOVERING,
        MUMBLE,
        OFFLINE,
        INACTIVE
    }

    /// <summary>
    /// The ServiceStatus for one particular service, team, and round.
    /// </summary>
    public sealed record RoundTeamServiceStatus(ServiceStatus Status,
        string? ErrorMessage,
        long TeamId,
        long ServiceId,
        long GameRoundId);
}
