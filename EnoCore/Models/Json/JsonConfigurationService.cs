using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationService
    {
        public string Name { get; set; }
        public int FlagsPerRound { get; set; }
        public int RunsPerFlag { get; set; }
        public int NoisesPerRound { get; set; }
        public int RunsPerNoise { get; set; }
        public int RunsPerHavok { get; set; }
        public int WeightFactor { get; set; }
    }
}
