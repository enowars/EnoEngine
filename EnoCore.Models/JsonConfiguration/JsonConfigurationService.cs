namespace EnoCore.Models.JsonConfiguration
{
    using System;
    using Json.Schema.Generation;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore.Models;
    using EnoCore.Models.CheckerApi;
    using EnoCore.Models.Schema;

    public record JsonConfigurationService
    {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        [Description("The id of the service.")]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        public long Id { get; init; }

        [Required]
        [Description("The title of the event.")]
        public string Name { get; init; }

        [Description("Whether the Service is active or not.")]
        public bool? Active { get; init; }

        [Required]
        [Description("The fully specified URL address for each checker")]
        [MinItems(1)]
        public Uri[] Checkers { get; init; }

        [Required]
        [Description("Multiplier for flags send per Round.")]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        public int FlagsPerRoundMultiplier { get; init; }

        [Required]
        [Description("Multiplier for havocs send per Round.")]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        public int HavocsPerRoundMultiplier { get; init; }

        [Required]
        [Description("Multiplier for noise send per Round.")]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        public int NoisesPerRoundMultiplier { get; init; }

        [Required]
        [Description("The weight for scoring this service.")]
        [Minimum(0)]
        [Maximum(uint.MaxValue)]
        public long WeightFactor { get; init; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    }
}
