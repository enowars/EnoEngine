using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationValidationException : Exception
    {
        public JsonConfigurationValidationException(string message) : base(message) { }
    }

    public sealed record JsonConfiguration
    {
        public string? Title { get; set; }
        public long FlagValidityInRounds { get; set; }
        public int CheckedRoundsPerRound { get; set; }
        public int RoundLengthInSeconds { get; set; }
        public string? DnsSuffix { get; set; }
        public int TeamSubnetBytesLength { get; set; }
        public string? FlagSigningKey { get; set; }
        public string? NoiseSigningKey { get; set; }
        [JsonPropertyName("Encoding")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FlagEncoding Encoding { get; set; }
        public List<JsonConfigurationTeam> Teams { get; set; } = new List<JsonConfigurationTeam>();
        public List<JsonConfigurationService> Services { get; set; } = new List<JsonConfigurationService>();

        public async Task<Configuration> ValidateAsync()
        {
            if (Title is null)
                throw new JsonConfigurationValidationException("title is null.");

            if (DnsSuffix is null)
                throw new JsonConfigurationValidationException("dnsSuffix is null.");

            if (FlagSigningKey is null)
                throw new JsonConfigurationValidationException("flagSigningKey is null.");

            if (RoundLengthInSeconds <= 0)
                throw new JsonConfigurationValidationException("roundLengthInSeconds is <= 0.");

            if (CheckedRoundsPerRound <= 0)
                throw new JsonConfigurationValidationException("checkedRoundsPerRound is <= 0.");

            if (FlagValidityInRounds <= 0)
                throw new JsonConfigurationValidationException("flagValidityInRounds is <= 0.");

            if (TeamSubnetBytesLength <= 0)
                throw new JsonConfigurationValidationException("teamSubnetBytesLength is <= 0.");

            List<ConfigurationTeam> teams = new();
            List<ConfigurationTeam> activeTeams = new();
            List<ConfigurationService> services = new();
            List<ConfigurationService> activeServices = new();
            Dictionary<long, string[]> checkers = new();

            foreach (var team in Teams)
            {
                if (teams.Where(t => t.Id == team.Id).Any())
                    throw new JsonConfigurationValidationException($"Duplicate teamId ({team.Id})");

                var validatedTeam = team.Validate(TeamSubnetBytesLength);
                teams.Add(validatedTeam);

                if (team.Active)
                {
                    activeTeams.Add(validatedTeam);
                }
            }

            foreach (var service in Services)
            {
                if (services.Where(s => s.Id == service.Id).Any())
                    throw new JsonConfigurationValidationException($"Duplicate serviceId ({service.Id})");

                var validatedService = await service.Validate();
                services.Add(validatedService);
                checkers.Add(service.Id, validatedService.Checkers);

                if (service.Active)
                {
                    activeServices.Add(validatedService);
                }
            }

            return new Configuration(Title,
                FlagValidityInRounds,
                CheckedRoundsPerRound,
                RoundLengthInSeconds,
                DnsSuffix,
                TeamSubnetBytesLength,
                FlagSigningKey,
                Encoding,
                teams,
                activeTeams,
                services,
                activeServices,
                checkers);
        }

        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static JsonConfiguration? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<JsonConfiguration>(json, JsonSerializerOptions);
        }
    }
}
