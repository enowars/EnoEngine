using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    public class Team
    {
#pragma warning disable CS8618
        public long Id { get; set; }
        public string Name { get; set; }
        public byte[] TeamSubnet { get; set; }
        public double TotalPoints { get; set; }
        public double AttackPoints { get; set; }
        public double DefensePoints { get; set; }
        public double ServiceLevelAgreementPoints { get; set; }
        public string? Address { get; set; }
        public long ServiceStatsId { get; set; }
        public List<TeamServicePoints> ServiceStats { get; set; }
        public bool Active { get; set; }
#pragma warning restore CS8618
    }
}
