using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnoCore.Models.Json
{
    public class EnoEngineScoreboardEntry
    {
        private readonly Team Team;

        public string Name { get => Team.Name; }
        public long TeamId { get => Team.Id; }
        public double TotalPoints { get => Team.TotalPoints; }
        public double AttackPoints { get => Team.AttackPoints; }
        public double LostDefensePoints { get => Team.LostDefensePoints; }
        public double ServiceLevelAgreementPoints { get => Team.ServiceLevelAgreementPoints; }
        public EnoEngineScoreboardEntryServiceDetails[] ServiceDetails { get; set; }

        public EnoEngineScoreboardEntry(Team team)
        {
            Team = team;
            ServiceDetails = team.ServiceStats.Select(s => new EnoEngineScoreboardEntryServiceDetails(s)).ToArray();
        }
    }
}
