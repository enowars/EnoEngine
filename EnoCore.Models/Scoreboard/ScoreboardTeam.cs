namespace EnoCore.Models.Scoreboard
{
    using System.ComponentModel.DataAnnotations;

    public class ScoreboardTeam
    {
        public ScoreboardTeam(string teamName, long teamId, string? logoUrl, string? countryCode, double totalScore, double attackScore, double defenseScore, double serviceLevelAgreementScore, ScoreboardTeamServiceDetails[] serviceDetails)
        {
            this.TeamName = teamName;
            this.TeamId = teamId;
            this.LogoUrl = logoUrl;
            this.CountryCode = countryCode;
            this.TotalScore = totalScore;
            this.AttackScore = attackScore;
            this.DefenseScore = defenseScore;
            this.ServiceLevelAgreementScore = serviceLevelAgreementScore;
            this.ServiceDetails = serviceDetails;
        }

        /// <summary>
        /// The name of the team.
        /// </summary>
        [Required]
        public string TeamName { get; init; }

        /// <summary>
        /// The id of the team.
        /// </summary>
        [Required]
        public long TeamId { get; init; }

        /// <summary>
        /// An URL with the team's logo, or null.
        /// </summary>
        public string? LogoUrl { get; init; }

        /// <summary>
        /// The ISO 3166-1 alpha-2 country code (uppercase), or null.
        /// </summary>
        public string? CountryCode { get; init; }

        /// <summary>
        /// The total Score of the team.
        /// </summary>
        [Required]
        public double TotalScore { get; init; }

        /// <summary>
        /// The attack Score of the team.
        /// </summary>
        [Required]
        public double AttackScore { get; init; }

        /// <summary>
        /// The defense Score of the team.
        /// </summary>
        [Required]
        public double DefenseScore { get; init; }

        /// <summary>
        /// The SLA Score of the team.
        /// </summary>
        [Required]
        public double ServiceLevelAgreementScore { get; init; }

        [Required]
        public ScoreboardTeamServiceDetails[] ServiceDetails { get; init; }
    }
}
