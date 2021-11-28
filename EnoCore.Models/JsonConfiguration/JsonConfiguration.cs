namespace EnoCore.Models.JsonConfiguration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using EnoCore.Models;
    using EnoCore.Models.Schema;
    using Json.Schema.Generation;


    /// <summary>
    /// The Configuration read from ctf.json.
    /// </summary>
    public class JsonConfiguration
    {
        public JsonConfiguration(string title, long flagValidityInRounds, int checkedRoundsPerRound, int roundLengthInSeconds, string dnsSuffix, int teamSubnetBytesLength, string flagSigningKey, FlagEncoding encoding, List<JsonConfigurationTeam> teams, List<JsonConfigurationService> services)
        {
            this.Title = title;
            this.FlagValidityInRounds = flagValidityInRounds;
            this.CheckedRoundsPerRound = checkedRoundsPerRound;
            this.RoundLengthInSeconds = roundLengthInSeconds;
            this.DnsSuffix = dnsSuffix;
            this.TeamSubnetBytesLength = teamSubnetBytesLength;
            this.FlagSigningKey = flagSigningKey;
            this.Encoding = encoding;
            this.Teams = teams;
            this.Services = services;
        }

        [Required]
        [Description("The title of the event.")]
        public string Title { get; init; }

        [Required]
        [Minimum(0)]
        [Maximum(uint.MaxValue)]
        [Description("Validity of a flag in rounds.")]
        public long FlagValidityInRounds { get; init; }

        [Required]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        [Description("Number of times a flag is checked per round.")]
        public int CheckedRoundsPerRound { get; init; }

        [Required]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        [Description("The length of one round in seconds.")]
        public int RoundLengthInSeconds { get; init; }

        [Required]
        [Description("The DNS Suffix.")]
        public string DnsSuffix { get; init; }

        [Required]
        [Description("Team Subnet byte length.")]
        [Minimum(0)]
        [Maximum(uint.MaxValue)]
        public int TeamSubnetBytesLength { get; init; }

        [Required]
        [Description("The Signing Key for the flags.")]
        public string FlagSigningKey { get; init; }

        [Required]
        [Description("Encoding of the flags")]
        public FlagEncoding Encoding { get; init; }

        [Required]
        [Description("All Teams participating in the CTF.")]
        [MinItems(1)]
        [UniqueItems(true)]
        public List<JsonConfigurationTeam> Teams { get; init; }

        [Required]
        [Description("All Services used in this CTF.")]
        [MinItems(1)]
        [UniqueItems(true)]
        public List<JsonConfigurationService> Services { get; init; }
    }
}
