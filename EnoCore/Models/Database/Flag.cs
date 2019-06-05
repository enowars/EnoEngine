﻿using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models
{
    public class Flag
    {
        public long Id { get; set; }
        public string StringRepresentation { get; set; }
        public long OwnerId { get; set; }
        public Team Owner { get; set; }
        public long ServiceId { get; set; }
        public Service Service { get; set; }
        public int RoundOffset { get; set; }
        public long GameRoundId { get; set; }
        public Round GameRound { get; set; }
        public CheckerTask PutTask { get; set; }
        public long? PutTaskId { get; set; }

        public override string ToString()
        {
            return $"{StringRepresentation}";
        }
    }
}
