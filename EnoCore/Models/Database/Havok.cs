using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    public class Havoc
    {
        public long Id { get; set; }
        public long OwnerId { get; set; }
        public Team Owner { get; set; }
        public long ServiceId { get; set; }
        public Service Service { get; set; }
        public long GameRoundId { get; set; }
        public Round GameRound { get; set; }
    }
}
