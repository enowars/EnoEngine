namespace EnoCore.Models.Database
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class Team
    {
#pragma warning disable CS8618
#pragma warning disable SA1516 // Elements should be separated by blank line
        public long Id { get; set; }
        public string Name { get; set; }
        public string? LogoUrl { get; set; }
        public string? CountryCode { get; set; }
        public byte[] TeamSubnet { get; set; }
        public double TotalPoints { get; set; }
        public double AttackPoints { get; set; }
        public double DefensePoints { get; set; }
        public double ServiceLevelAgreementPoints { get; set; }
        public string? Address { get; set; }
        public long ServiceStatsId { get; set; }
        public List<TeamServicePoints> TeamServicePoints { get; set; }
        public bool Active { get; set; }
#pragma warning restore SA1516 // Elements should be separated by blank line
#pragma warning restore CS8618

        public override string ToString()
        {
            return $"Team(Id={this.Id}, Name={this.Name}, Address={this.Address}, Active={this.Active})";
        }
    }
}
