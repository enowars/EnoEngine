using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    /// <summary>
    /// PK: FlagServiceId, sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset, sf.AttackerTeamId
    /// Flag FK: FlagServiceId, FlagRoundId, FlagOwnerId, FlagRoundOffset
    /// </summary>
    public class SubmittedFlag
    {
#pragma warning disable CS8618
        public long FlagServiceId { get; set; }
        public long FlagOwnerId { get; set; }
        public long FlagRoundId { get; set; }
        public int FlagRoundOffset { get; set; }
        public long AttackerTeamId { get; set; }
        public long RoundId { get; set; }
        public long SubmissionsCount { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public virtual Flag Flag { get; set; }
        public virtual Team AttackerTeam { get; set; }
        public virtual Round Round { get; set; }
#pragma warning restore CS8618
    }
}
