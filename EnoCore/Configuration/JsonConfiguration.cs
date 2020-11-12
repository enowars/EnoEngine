namespace EnoCore.Configuration
{
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
    using EnoCore.Models;

    public sealed record JsonConfiguration(
        string? Title,
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
            return JsonSerializer.Deserialize<JsonConfiguration>(json, EnoCoreUtil.CamelCaseEnumConverterOptions);
        }

        public async Task<Configuration> ValidateAsync()
        {
            if (this.Title is null)
            {
                throw new JsonConfigurationValidationException("title must not be null.");
            }

            if (this.DnsSuffix is null)
            {
                throw new JsonConfigurationValidationException("dnsSuffix must not be  null.");
            }

            if (this.FlagSigningKey is null)
            {
                throw new JsonConfigurationValidationException("flagSigningKey must not be  null.");
            }

            if (this.RoundLengthInSeconds <= 0)
            {
                throw new JsonConfigurationValidationException("roundLengthInSeconds must not be  <= 0.");
            }

            if (this.CheckedRoundsPerRound <= 0)
            {
                throw new JsonConfigurationValidationException("checkedRoundsPerRound must not be <= 0.");
            }

            if (this.FlagValidityInRounds <= 0)
            {
                throw new JsonConfigurationValidationException("flagValidityInRounds must not be <= 0.");
            }

            if (this.TeamSubnetBytesLength <= 0)
            {
                throw new JsonConfigurationValidationException("teamSubnetBytesLength must not be <= 0.");
            }

            if (this.Teams is null)
            {
                throw new JsonConfigurationValidationException("teams must not null.");
            }

            if (this.Services is null)
            {
                throw new JsonConfigurationValidationException("services must not null.");
            }

            if (this.Teams.Count == 0)
            {
                throw new JsonConfigurationValidationException("teams must not be empty.");
            }

            if (this.Services.Count == 0)
            {
                throw new JsonConfigurationValidationException("services must not be empty.");
            }

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
            if (this.Id == 0)
            {
                throw new JsonConfigurationServiceValidationException("Service id must not be 0.");
            }

            if (this.Name is null)
            {
                throw new JsonConfigurationServiceValidationException($"Service name must not be null (service {this.Id}).");
            }

            if (this.Checkers is null)
            {
                throw new JsonConfigurationServiceValidationException($"Service checkers must not be null (service {this.Id}).");
            }

            if (this.Checkers.Length == 0)
            {
                throw new JsonConfigurationServiceValidationException($"Service checkers must not be empty (service {this.Id}).");
            }

            // Ask the checker how many flags/noises/havocs the service wants
            CheckerInfoMessage? infoMessage;
            try
            {
                using var client = new HttpClient();
                var cancelSource = new CancellationTokenSource();
                cancelSource.CancelAfter(5 * 1000);
                var responseString = await client.GetStringAsync($"{this.Checkers[0]}/service", cancelSource.Token);
                infoMessage = JsonSerializer.Deserialize<CheckerInfoMessage>(responseString, EnoCoreUtil.CamelCaseEnumConverterOptions);
            }
            catch (Exception e)
            {
                throw new JsonConfigurationServiceValidationException($"Service checker failed to respond to info request (service {this.Id}).", e);
            }

            if (infoMessage is null)
            {
                throw new JsonConfigurationServiceValidationException($"Service checker failed to respond to info request (service {this.Id}).");
            }

            return new(
                this.Id,
                this.Name,
                this.FlagsPerRoundMultiplier * infoMessage.FlagCount,
                this.NoisesPerRoundMultiplier * infoMessage.NoiseCount,
                this.HavocsPerRoundMultiplier * infoMessage.HavocCount,
                infoMessage.FlagCount,
                this.WeightFactor,
                this.Active,
                this.Checkers);
        }
    }

    public record JsonConfigurationTeam(
        long Id,
        string? Name,
        string? Address,
        string? TeamSubnet,
        string? LogoUrl,
        string? CountryFlagUrl,
        bool Active = true)
    {
        public ConfigurationTeam Validate(int subnetBytesLength)
        {
            if (this.Id == 0)
            {
                throw new JsonConfigurationTeamValidationException("Team id must not be 0.");
            }

            if (this.Name is null)
            {
                throw new JsonConfigurationTeamValidationException($"Team name must not be null (team {this.Id}).");
            }

            if (this.TeamSubnet is null)
            {
                throw new JsonConfigurationTeamValidationException($"Team subnet must not be null (team {this.Id}).");
            }

            IPAddress ip;
            try
            {
                ip = IPAddress.Parse(this.TeamSubnet);
            }
            catch (Exception e)
            {
                throw new JsonConfigurationTeamValidationException($"Team subnet is no valid IP address (team {this.Id}).", e);
            }

            byte[] teamSubnet = new byte[subnetBytesLength];
            Array.Copy(ip.GetAddressBytes(), teamSubnet, subnetBytesLength);

            return new(this.Id,
                this.Name,
                this.Address,
                teamSubnet,
                this.LogoUrl,
                this.CountryFlagUrl,
                this.Active);
        }
    }
}
