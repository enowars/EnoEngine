using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace EnoCore.Models
{
    public class SubmittedFlag
    {
        public long FlagServiceId { get; set; }
        public long FlagOwnerId { get; set; }
        public long FlagRoundId { get; set; }
        public int FlagRoundOffset { get; set; }
        public long AttackerTeamId { get; set; }
        public long RoundId { get; set; }
        public long SubmissionsCount { get; set; }

        [ForeignKey("FlagServiceId, FlagRoundId, FlagOwnerId, FlagRoundOffset")]
        public virtual Flag Flag { get; set; }
        public virtual Team AttackerTeam { get; set; }
        public virtual Round Round { get; set; }
    }
}
