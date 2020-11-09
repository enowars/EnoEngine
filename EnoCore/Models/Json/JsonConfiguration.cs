using EnoCore.Models.Database;
using EnoCore.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationValidationException : Exception
    {
        public JsonConfigurationValidationException(string message) : base(message) { }
    }

    public record JsonConfiguration
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

            List<ConfigurationTeam> teams = new();
            List<ConfigurationService> services = new();
            Dictionary<long, string[]> checkers = new();

            foreach (var team in Teams)
                teams.Add(team.Validate());
            // TODO ensure team ids are unique

            foreach (var service in Services)
            {
                services.Add(await service.Validate());
                checkers.Add(service.Id, service.Checkers!);
                // TODO ensure service ids are unique
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
                services,
                checkers);
        }
    }
}
