using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class JsonConfiguration
    {
        public long FlagValidityInRounds { get; set; }
        public int CheckedRoundsPerRound { get; set; }
        public int RoundLengthInSeconds { get; set; }
        public List<JsonConfigurationTeam> Teams { get; set; } = new List<JsonConfigurationTeam>();
        public List<JsonConfigurationService> Services { get; set; } = new List<JsonConfigurationService>();
    }
}
