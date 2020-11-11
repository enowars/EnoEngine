using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationServiceValidationException : Exception
    {
        public JsonConfigurationServiceValidationException(string message) : base(message) { }
        public JsonConfigurationServiceValidationException(string message, Exception inner) : base(message, inner) { }
    }

    public class JsonConfigurationService
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public int FlagsPerRoundMultiplier { get; set; }
        public int NoisesPerRoundMultiplier { get; set; }
        public int HavocsPerRoundMultiplier { get; set; }
        public long WeightFactor { get; set; }
        public bool Active { get; set; } = true;
        public string[]? Checkers { get; set; }

        public async Task<ConfigurationService> Validate()
        {
            if (Id == 0)
                throw new JsonConfigurationServiceValidationException("Service id must not be 0.");

            if (Name is null)
                throw new JsonConfigurationServiceValidationException($"Service name must not be null (service {Id}).");

            if (Checkers is null)
                throw new JsonConfigurationServiceValidationException($"Service checkers must not be null (service {Id}).");

            if (Checkers.Length == 0)
                throw new JsonConfigurationServiceValidationException($"Service checkers must not be empty (service {Id}).");

            // Ask the checker how many flags/noises/havocs the service wants
            CheckerInfoMessage? infoMessage;
            try
            {
                using var client = new HttpClient();
                var cancelSource = new CancellationTokenSource();
                cancelSource.CancelAfter(5 * 1000);
                var responseString = await client.GetStringAsync($"{Checkers[0]}/service", cancelSource.Token);
                infoMessage = JsonSerializer.Deserialize<CheckerInfoMessage>(responseString,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            catch (Exception e)
            {
                throw new JsonConfigurationServiceValidationException($"Service checker failed to respond to info request (service {Id}).", e);
            }

            if (infoMessage is null)
                throw new JsonConfigurationServiceValidationException($"Service checker failed to respond to info request (service {Id}).");

            return new(Id,
                Name,
                FlagsPerRoundMultiplier * infoMessage.FlagCount,
                NoisesPerRoundMultiplier * infoMessage.NoiseCount,
                HavocsPerRoundMultiplier * infoMessage.HavocCount,
                infoMessage.FlagCount,
                WeightFactor,
                Active,
                Checkers);
        }
    }
}
