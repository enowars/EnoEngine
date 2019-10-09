using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models
{
    public enum ServiceStatus
    {
        CheckerError,
        Ok,
        Recovering,
        Mumble,
        Down
    }

    public class RoundTeamServiceState
    {
        public ServiceStatus Status { get; set; }
        public long TeamId { get; set; }
        public Team Team { get; set; }
        public long ServiceId { get; set; }
        public Service Service { get; set; }
        public long GameRoundId { get; set; }
        public Round GameRound { get; set; }
    }
}
