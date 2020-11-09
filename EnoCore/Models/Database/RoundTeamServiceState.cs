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

    public record RoundTeamServiceState(ServiceStatus Status,
        string? ErrorMessage,
        long TeamId,
        long ServiceId,
        long GameRoundId)
    {
        public virtual Team? Team { get; set; }
        public virtual Service? Service { get; set; }
        public virtual Round? GameRound { get; set; }
    }
}
