using System.ComponentModel.DataAnnotations;

namespace EnoCore.Scoreboard
{
    public class ScoreboardFirstBlood
    {
        public ScoreboardFirstBlood(long teamId, string teamName, string timestamp, long roundId, long flagVariantId)
        {
            this.TeamId = teamId;
            this.TeamName = teamName;
            this.Timestamp = timestamp;
            this.RoundId = roundId;
            this.FlagVariantId = flagVariantId;
        }

        /// <summary>
        /// The id of the team that scored the firstblood.
        /// </summary>
        [Required]
        public long TeamId { get; init; }

        /// <summary>
        /// The name of the team that scored the firstblood.
        /// </summary>
        [Required]
        public string TeamName { get; init; }

        /// <summary>
        /// Timestamp according to ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ") in UTC.
        /// </summary>
        [Required]
        public string Timestamp { get; init; }

        /// <summary>
        ///  The id of the round in which the firstblood was submitted.
        /// </summary>
        [Required]
        public long RoundId { get; init; }

        /// <summary>
        ///  The id of the variant.
        /// </summary>
        [Required]
        public long FlagVariantId { get; init; }


    }
}
