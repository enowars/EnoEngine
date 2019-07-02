using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace EnoCore.Models
{
    public class SubmittedFlag
    {
        public long Id { get; set; }
        public long FlagId { get; set; }
        public Flag Flag { get; set; }
        public long AttackerTeamId { get; set; }
        public Team AttackerTeam { get; set; }
        public long RoundId { get; set; }
        public Round Round { get; set; }
        public long SubmissionsCount { get; set; }
    }
}
