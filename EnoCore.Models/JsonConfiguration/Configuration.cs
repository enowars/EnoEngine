namespace EnoCore.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore.Models;
    using EnoCore.Models.CheckerApi;
    using EnoCore.Models.Database;
    using EnoCore.Models.JsonConfiguration;

    public sealed record Configuration(
        string Title,
        long FlagValidityInRounds,
        int CheckedRoundsPerRound,
        int RoundLengthInSeconds,
        string DnsSuffix,
        int TeamSubnetBytesLength,
        string FlagSigningKey,
        FlagEncoding Encoding,
        List<ConfigurationTeam> Teams,
        List<ConfigurationTeam> ActiveTeams,
        List<ConfigurationService> Services,
        List<ConfigurationService> ActiveServices,
        Dictionary<long, string[]> Checkers)
    {
        public static async Task<Configuration> Validate(JsonConfiguration jsonConfiguration)
        {
            if (jsonConfiguration.Title is null)
            {
                throw new JsonConfigurationValidationException("title must not be null.");
            }

            if (jsonConfiguration.DnsSuffix is null)
            {
                throw new JsonConfigurationValidationException("dnsSuffix must not be null.");
            }

            if (jsonConfiguration.FlagSigningKey is null)
            {
                throw new JsonConfigurationValidationException("flagSigningKey must not be null.");
            }

            if (jsonConfiguration.RoundLengthInSeconds <= 0)
            {
                throw new JsonConfigurationValidationException("roundLengthInSeconds must not be <= 0.");
            }

            if (jsonConfiguration.CheckedRoundsPerRound <= 0)
            {
                throw new JsonConfigurationValidationException("checkedRoundsPerRound must not be <= 0.");
            }

            if (jsonConfiguration.FlagValidityInRounds <= 0)
            {
                throw new JsonConfigurationValidationException("flagValidityInRounds must not be <= 0.");
            }

            if (jsonConfiguration.TeamSubnetBytesLength <= 0)
            {
                throw new JsonConfigurationValidationException("teamSubnetBytesLength must not be <= 0.");
            }

            if (jsonConfiguration.Teams is null)
            {
                throw new JsonConfigurationValidationException("teams must not null.");
            }

            if (jsonConfiguration.Services is null)
            {
                throw new JsonConfigurationValidationException("services must not null.");
            }

            if (jsonConfiguration.Teams.Count == 0)
            {
                throw new JsonConfigurationValidationException("teams must not be empty.");
            }

            if (jsonConfiguration.Services.Count == 0)
            {
                throw new JsonConfigurationValidationException("services must not be empty.");
            }

            List<ConfigurationTeam> teams = new();
            List<ConfigurationTeam> activeTeams = new();
            List<ConfigurationService> services = new();
            List<ConfigurationService> activeServices = new();
            Dictionary<long, string[]> checkers = new();

            foreach (var team in jsonConfiguration.Teams)
            {
                if (teams.Where(t => t.Id == team.Id).Any())
                {
                    throw new JsonConfigurationValidationException($"Duplicate teamId ({team.Id})");
                }

                var validatedTeam = ConfigurationTeam.Validate(team, jsonConfiguration.TeamSubnetBytesLength);
                teams.Add(validatedTeam);

                if (team.Active)
                {
                    activeTeams.Add(validatedTeam);
                }
            }

            foreach (var service in jsonConfiguration.Services)
            {
                if (services.Where(s => s.Id == service.Id).Any())
                {
                    throw new JsonConfigurationValidationException($"Duplicate serviceId ({service.Id})");
                }

                var validatedService = await ConfigurationService.Validate(service);
                services.Add(validatedService);
                checkers.Add(service.Id, validatedService.Checkers);

                if (service.Active)
                {
                    activeServices.Add(validatedService);
                }
            }

            return new(
                jsonConfiguration.Title,
                jsonConfiguration.FlagValidityInRounds,
                jsonConfiguration.CheckedRoundsPerRound,
                jsonConfiguration.RoundLengthInSeconds,
                jsonConfiguration.DnsSuffix,
                jsonConfiguration.TeamSubnetBytesLength,
                jsonConfiguration.FlagSigningKey,
                jsonConfiguration.Encoding,
                teams,
                activeTeams,
                services,
                activeServices,
                checkers);
        }
    }

    public sealed record ConfigurationTeam(
        long Id,
        string Name,
        string? Address,
        byte[] TeamSubnet,
        string? LogoUrl,
        string? CountryCode,
        bool Active)
    {
        public static ConfigurationTeam Validate(JsonConfigurationTeam jsonConfigurationTeam, int subnetBytesLength)
        {
            if (jsonConfigurationTeam.Id == 0)
            {
                throw new JsonConfigurationTeamValidationException("Team id must not be 0.");
            }

            if (jsonConfigurationTeam.Name is null)
            {
                throw new JsonConfigurationTeamValidationException($"Team name must not be null (team {jsonConfigurationTeam.Id}).");
            }

            if (jsonConfigurationTeam.TeamSubnet is null)
            {
                throw new JsonConfigurationTeamValidationException($"Team subnet must not be null (team {jsonConfigurationTeam.Id}).");
            }

            IPAddress ip;
            try
            {
                ip = IPAddress.Parse(jsonConfigurationTeam.TeamSubnet);
            }
            catch (Exception e)
            {
                throw new JsonConfigurationTeamValidationException($"Team subnet is no valid IP address (team {jsonConfigurationTeam.Id}).", e);
            }

            byte[] teamSubnet = new byte[subnetBytesLength];
            Array.Copy(ip.GetAddressBytes(), teamSubnet, subnetBytesLength);

            return new(jsonConfigurationTeam.Id,
                jsonConfigurationTeam.Name,
                jsonConfigurationTeam.Address,
                teamSubnet,
                jsonConfigurationTeam.LogoUrl,
                jsonConfigurationTeam.CountryCode,
                jsonConfigurationTeam.Active);
        }
    }

    public sealed record ConfigurationService(
        long Id,
        string Name,
        int FlagsPerRound,
        int NoisesPerRound,
        int HavocsPerRound,
        int FlagVariants,
        int NoiseVariants,
        int HavocVariants,
        long WeightFactor,
        bool Active,
        string[] Checkers)
    {
        public static async Task<ConfigurationService> Validate(JsonConfigurationService jsonConfigurationService)
        {
            if (jsonConfigurationService.Id == 0)
            {
                throw new JsonConfigurationServiceValidationException("Service id must not be 0.");
            }

            if (jsonConfigurationService.Name is null)
            {
                throw new JsonConfigurationServiceValidationException($"Service name must not be null (service {jsonConfigurationService.Id}).");
            }

            if (jsonConfigurationService.Checkers is null)
            {
                throw new JsonConfigurationServiceValidationException($"Service checkers must not be null (service {jsonConfigurationService.Id}).");
            }

            if (jsonConfigurationService.Checkers.Length == 0)
            {
                throw new JsonConfigurationServiceValidationException($"Service checkers must not be empty (service {jsonConfigurationService.Id}).");
            }

            // Ask the checker how many flags/noises/havocs the service wants
            CheckerInfoMessage? infoMessage;
            try
            {
                using var client = new HttpClient();
                var cancelSource = new CancellationTokenSource();
                cancelSource.CancelAfter(5 * 1000);
                var responseString = await client.GetStringAsync($"{jsonConfigurationService.Checkers[0]}/service", cancelSource.Token);
                infoMessage = JsonSerializer.Deserialize<CheckerInfoMessage>(responseString, EnoCoreUtil.CamelCaseEnumConverterOptions);
            }
            catch (Exception e)
            {
                throw new JsonConfigurationServiceValidationException($"Service checker failed to respond to info request (service {jsonConfigurationService.Id}).", e);
            }

            if (infoMessage is null)
            {
                throw new JsonConfigurationServiceValidationException($"Service checker failed to respond to info request (service {jsonConfigurationService.Id}).");
            }

            return new(
                jsonConfigurationService.Id,
                jsonConfigurationService.Name,
                jsonConfigurationService.FlagsPerRoundMultiplier * infoMessage.FlagVariants,
                jsonConfigurationService.NoisesPerRoundMultiplier * infoMessage.NoiseVariants,
                jsonConfigurationService.HavocsPerRoundMultiplier * infoMessage.HavocVariants,
                infoMessage.FlagVariants,
                infoMessage.NoiseVariants,
                infoMessage.HavocVariants,
                jsonConfigurationService.WeightFactor,
                jsonConfigurationService.Active,
                jsonConfigurationService.Checkers);
        }
    }
}
