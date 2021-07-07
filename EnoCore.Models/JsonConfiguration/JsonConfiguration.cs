namespace EnoCore.Models.JsonConfiguration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using EnoCore.Models;

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
        [Range(minimum: 0, long.MaxValue)]
        [Description("Validity of a flag in rounds.")]
        public long FlagValidityInRounds { get; init; }

        [Required]
        [Range(minimum: 1, long.MaxValue)]
        [Description("Number of times a flag is checked per round.")]
        public int CheckedRoundsPerRound { get; init; }

        [Required]
        [Range(minimum: 1, long.MaxValue)]
        [Description("The length of one round in seconds.")]
        public int RoundLengthInSeconds { get; init; }

        [Required]
        [Description("The DNS Suffix.")]
        public string DnsSuffix { get; init; }

        [Required]
        [Description("Team Subnet byte length.")]
        public int TeamSubnetBytesLength { get; init; }

        [Required]
        [Description("The Signing Key for the flags.")]
        public string FlagSigningKey { get; init; }

        [Required]
        [Description("Encoding of the flags")]
        [EnumDataType(typeof(FlagEncoding))]
        public FlagEncoding Encoding { get; init; }

        [Required]
        [Description("All Teams participating in the CTF.")]
        [MinLength(1)]
        public List<JsonConfigurationTeam> Teams { get; init; }

        [Required]
        [Description("All Services used in this CTF.")]
        [MinLength(1)]
        public List<JsonConfigurationService> Services { get; init; }
    }
}
