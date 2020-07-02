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

    public class RoundTeamServiceState
    {
#pragma warning disable CS8618
        public ServiceStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public long TeamId { get; set; }

        public Team Team { get; set; }
        public long ServiceId { get; set; }
        public Service Service { get; set; }
        public long GameRoundId { get; set; }
        public Round GameRound { get; set; }
#pragma warning restore CS8618
    }
}
