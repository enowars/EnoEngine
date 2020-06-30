using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationService
    {
#pragma warning disable CS8618
        public long Id { get; set; }
        public string Name { get; set; }
        public long FlagsPerRound { get; set; }
        public long NoisesPerRound { get; set; }
        public long HavocsPerRound { get; set; }
        public long WeightFactor { get; set; }
        public bool Active { get; set; }
        public string[] Checkers { get; set; }
#pragma warning restore CS8618
    }
}
