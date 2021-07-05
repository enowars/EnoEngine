namespace EnoCore.Models.Scoreboard
{
    using System.ComponentModel.DataAnnotations;

    public class ScoreboardService
    {
        public ScoreboardService(long serviceId, string serviceName, long flagVariants, ScoreboardFirstBlood[] firstBloods)
        {
            this.ServiceId = serviceId;
            this.ServiceName = serviceName;
            this.FlagVariants = flagVariants;
            this.FirstBloods = firstBloods;
        }

        /// <summary>
        /// The id of the service.
        /// </summary>
        [Required]
        public long ServiceId { get; init; }

        /// <summary>
        /// The name of the service.
        /// </summary>
        [Required]
        public string ServiceName { get; init; }

        /// <summary>
        /// The amount of different flag variants.
        /// TODO: scrape it and only provide an array of flags with First Bloods as property.
        /// </summary>
        [Required]
        public long FlagVariants { get; init; }

        /// <summary>
        /// The amount of different flag variants.
        /// </summary>
        [Required]
        public ScoreboardFirstBlood[] FirstBloods { get; init; }
    }
}
