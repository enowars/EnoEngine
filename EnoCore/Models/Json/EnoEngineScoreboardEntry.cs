namespace EnoCore.Models.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using EnoCore.Models.Database;

    public class EnoEngineScoreboardEntry
    {
        private readonly Team team;

        public EnoEngineScoreboardEntry(Team team)
        {
            this.team = team;
            this.ServiceDetails = team.ServiceStats.Select(s => new EnoEngineScoreboardEntryServiceDetails(s)).ToArray();
        }

        protected EnoEngineScoreboardEntry()
        {
            this.team = default!;
            this.ServiceDetails = default!;
        }

        public string Name { get => this.team.Name; }
        public long TeamId { get => this.team.Id; }
        public double TotalPoints { get => this.team.TotalPoints; }
        public double AttackPoints { get => this.team.AttackPoints; }
        public double LostDefensePoints { get => this.team.DefensePoints; }
        public double ServiceLevelAgreementPoints { get => this.team.ServiceLevelAgreementPoints; }
        public EnoEngineScoreboardEntryServiceDetails[] ServiceDetails { get; set; }
    }
}
