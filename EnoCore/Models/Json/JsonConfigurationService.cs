using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationService
    {
#pragma warning disable CS8618
        public long Id { get; set; }
        public string Name { get; set; }
        [JsonPropertyName("FlagsPerRound")]
        public long ConfigFlagsPerRound { get; set; }
        [JsonPropertyName("NoisesPerRound")]
        public long ConfigNoisesPerRound { get; set; }
        [JsonPropertyName("HavocsPerRound")]
        public long ConfigHavocsPerRound { get; set; }
        [JsonIgnore]
        public long FetchedFlagsPerRound { get; set; } = 1;
        [JsonIgnore]
        public long FetchedNoisesPerRound { get; set; } = 1;
        [JsonIgnore]
        public long FetchedHavocsPerRound { get; set; } = 1;
        [JsonIgnore]
        public long FlagsPerRound { get => ConfigFlagsPerRound*FetchedFlagsPerRound; }
        [JsonIgnore]
        public long NoisesPerRound { get => ConfigNoisesPerRound*FetchedNoisesPerRound; }
        [JsonIgnore]
        public long HavocsPerRound { get => ConfigHavocsPerRound*FetchedHavocsPerRound; }
        public long WeightFactor { get; set; }
        public bool Active { get; set; }
        public string[] Checkers { get; set; }
#pragma warning restore CS8618
    }
}
