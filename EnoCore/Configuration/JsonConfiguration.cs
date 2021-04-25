namespace EnoCore.Configuration
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
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Schema;
    using Newtonsoft.Json.Schema.Generation;
    using Newtonsoft.Json.Serialization;

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
        [Description("Team Subnet bye lenght.")]
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

        public static JsonConfiguration? Deserialize(string json)
        {
            JsonTextReader reader = new JsonTextReader(new StringReader(json));

            JSchemaValidatingReader validatingReader = new JSchemaValidatingReader(reader);
            validatingReader.Schema = EnoCoreUtil.GenerateSchema();

            IList<string> errorMessages = new List<string>();
            validatingReader.ValidationEventHandler += (o, a) => errorMessages.Add(a.Message);

            Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
            JsonConfiguration? parsedJson = serializer.Deserialize<JsonConfiguration>(validatingReader);

            bool isValid = errorMessages.Count == 0;
            if (!isValid)
            {
                throw new AggregateException(errorMessages.Select(e => new JsonConfigurationValidationException(e)));
            }

            return parsedJson;
        }

        public async Task<Configuration> ValidateAsync()
        {
            List<ConfigurationTeam> teams = new();
            List<ConfigurationTeam> activeTeams = new();
            List<ConfigurationService> services = new();
            List<ConfigurationService> activeServices = new();
            Dictionary<long, string[]> checkers = new();

            foreach (var team in this.Teams)
            {
                if (teams.Where(t => t.Id == team.Id).Any())
                {
                    throw new JsonConfigurationValidationException($"Duplicate teamId ({team.Id})");
                }

                var validatedTeam = team.Validate(this.TeamSubnetBytesLength);
                teams.Add(validatedTeam);

                if (team.Active)
                {
                    activeTeams.Add(validatedTeam);
                }
            }

            foreach (var service in this.Services)
            {
                if (services.Where(s => s.Id == service.Id).Any())
                {
                    throw new JsonConfigurationValidationException($"Duplicate serviceId ({service.Id})");
                }

                var validatedService = await service.Validate();
                services.Add(validatedService);
                checkers.Add(service.Id, validatedService.Checkers);

                if (service.Active)
                {
                    activeServices.Add(validatedService);
                }
            }

            return new(
                this.Title,
                this.FlagValidityInRounds,
                this.CheckedRoundsPerRound,
                this.RoundLengthInSeconds,
                this.DnsSuffix,
                this.TeamSubnetBytesLength,
                this.FlagSigningKey,
                this.Encoding,
                teams,
                activeTeams,
                services,
                activeServices,
                checkers);
        }
    }
}
