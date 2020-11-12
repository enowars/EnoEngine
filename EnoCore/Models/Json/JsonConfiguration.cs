using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationValidationException : Exception
    {
        public JsonConfigurationValidationException(string message) : base(message) { }
        public JsonConfigurationValidationException(string message, Exception inner) : base(message, inner) { }
    }

    public class JsonConfigurationTeamValidationException : JsonConfigurationValidationException
    {
        public JsonConfigurationTeamValidationException(string message) : base(message) { }
        public JsonConfigurationTeamValidationException(string message, Exception inner) : base(message, inner) { }
    }

    public class JsonConfigurationServiceValidationException : JsonConfigurationValidationException
    {
        public JsonConfigurationServiceValidationException(string message) : base(message) { }
        public JsonConfigurationServiceValidationException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed record JsonConfiguration(string? Title,
        long FlagValidityInRounds,
        int CheckedRoundsPerRound,
        int RoundLengthInSeconds,
        string? DnsSuffix,
        int TeamSubnetBytesLength,
        string? FlagSigningKey,
        FlagEncoding Encoding,
        List<JsonConfigurationTeam>? Teams,
        List<JsonConfigurationService>? Services)
    {
        public static JsonConfiguration? Deserialize(string json)
        {
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Deserialize<JsonConfiguration>(json, jsonSerializerOptions);
        }

        public async Task<Configuration> ValidateAsync()
        {
            if (Title is null)
                throw new JsonConfigurationValidationException("title must not be null.");

            if (DnsSuffix is null)
                throw new JsonConfigurationValidationException("dnsSuffix must not be  null.");

            if (FlagSigningKey is null)
                throw new JsonConfigurationValidationException("flagSigningKey must not be  null.");

            if (RoundLengthInSeconds <= 0)
                throw new JsonConfigurationValidationException("roundLengthInSeconds must not be  <= 0.");

            if (CheckedRoundsPerRound <= 0)
                throw new JsonConfigurationValidationException("checkedRoundsPerRound must not be <= 0.");

            if (FlagValidityInRounds <= 0)
                throw new JsonConfigurationValidationException("flagValidityInRounds must not be <= 0.");

            if (TeamSubnetBytesLength <= 0)
                throw new JsonConfigurationValidationException("teamSubnetBytesLength must not be <= 0.");

            if (Teams is null)
                throw new JsonConfigurationValidationException("teams must not null.");

            if (Services is null)
                throw new JsonConfigurationValidationException("services must not null.");

            if (Teams.Count == 0)
                throw new JsonConfigurationValidationException("teams must not be empty.");

            if (Services.Count == 0)
                throw new JsonConfigurationValidationException("services must not be empty.");

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
    }

    public record JsonConfigurationService(long Id,
        string? Name,
        int FlagsPerRoundMultiplier,
        int NoisesPerRoundMultiplier,
        int HavocsPerRoundMultiplier,
        long WeightFactor,
        string[]? Checkers,
        bool Active = true)
    {
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

    public record JsonConfigurationTeam(long Id,
        string? Name,
        string? Address,
        string? TeamSubnet,
        string? LogoUrl,
        string? FlagUrl,
        bool Active = true)
    {
        public ConfigurationTeam Validate(int subnetBytesLength)
        {
            if (Id == 0)
                throw new JsonConfigurationTeamValidationException("Team id must not be 0.");

            if (Name is null)
                throw new JsonConfigurationTeamValidationException($"Team name must not be null (team {Id}).");

            if (TeamSubnet is null)
                throw new JsonConfigurationTeamValidationException($"Team subnet must not be null (team {Id}).");

            IPAddress ip;
            try
            {
                ip = IPAddress.Parse(TeamSubnet);
            }
            catch (Exception e)
            {
                throw new JsonConfigurationTeamValidationException($"Team subnet is no valid IP address (team {Id}).", e);
            }

            byte[] teamSubnet = new byte[subnetBytesLength];
            Array.Copy(ip.GetAddressBytes(), teamSubnet, subnetBytesLength);

            return new(Id,
                Name,
                Address,
                teamSubnet,
                LogoUrl,
                FlagUrl,
                Active);
        }
    }
}
