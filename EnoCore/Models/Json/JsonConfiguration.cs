using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class JsonConfiguration
    {
#pragma warning disable CS8618
        public long FlagValidityInRounds { get; set; }
        public int CheckedRoundsPerRound { get; set; }
        public int RoundLengthInSeconds { get; set; }
        public string DnsSuffix { get; set; }
        public int TeamSubnetBytesLength { get; set; }
        public List<JsonConfigurationTeam> Teams { get; set; } = new List<JsonConfigurationTeam>();
        public List<JsonConfigurationService> Services { get; set; } = new List<JsonConfigurationService>();
        [JsonIgnore]
        public Dictionary<long, string[]> Checkers { get; set; } = new Dictionary<long, string[]>();
#pragma warning restore CS8618

        public void BuildCheckersDict()
        {
            foreach (var service in Services)
            {
                Checkers.Add(service.Id, service.Checkers);
            }
        }
    }
}
