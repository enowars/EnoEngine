using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace EnoCore.Models
{
    public class Team
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }
        public string Name { get; set; }
        public string TeamSubnet { get; set; }
        public double TotalPoints { get; set; }
        public double AttackPoints { get; set; }
        public double LostDefensePoints { get; set; }
        public double ServiceLevelAgreementPoints { get; set; }
        public List<RoundTeamServiceState> ServiceDetails { get; set; }
        public long ServiceStatsId { get; set; }
        public List<ServiceStats> ServiceStats { get; set; }
        public List<CheckerTask> CheckerTasks { get; set; }
        public bool Active { get; set; }
    }
}
