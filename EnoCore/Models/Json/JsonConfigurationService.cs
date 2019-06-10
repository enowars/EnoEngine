using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationService
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int FlagsPerRound { get; set; }
        public int NoisesPerRound { get; set; }
        public int HavocsPerRound { get; set; }
        public int WeightFactor { get; set; }
        public bool Active { get; set; }
        public string[] Checkers { get; set; }
    }
}
