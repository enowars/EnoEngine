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
    using Json.Schema;

    public record Configuration(
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
        public static async Task<Configuration> LoadAndValidate(string config)
        {
            // Statically validate based on the schema
            var schema = EnoCoreUtil.GenerateSchema();
            var options = new ValidationOptions
            {
                OutputFormat = OutputFormat.Basic,
                RequireFormatValidation = true,
            };
            var validationResults = schema.Validate(
                JsonDocument.Parse(config).RootElement, options);

            if (!validationResults.IsValid)
            {
                throw new AggregateException(validationResults.NestedResults.Append(validationResults).Select(e => new JsonConfigurationValidationException(e.SchemaLocation + ": " + e.Message)));
            }

            var jsonConfiguration = JsonSerializer.Deserialize<JsonConfiguration>(config, EnoCoreUtil.SerializerOptions);
            if (jsonConfiguration is null)
            {
                throw new JsonException("Could not be deserialized");
            }

            return await LiveValidate(jsonConfiguration);
        }

        public static async Task<Configuration> LiveValidate(JsonConfiguration jsonConfiguration)
        {
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

                if (team.Active ?? false)
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

                if (service.Active ?? false)
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
                jsonConfigurationTeam.LogoUrl != null ? jsonConfigurationTeam.LogoUrl.ToString() : null,
                jsonConfigurationTeam.CountryCode,
                jsonConfigurationTeam.Active ?? false);
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
            // Ask the checker how many flags/noises/havocs the service wants
            CheckerInfoMessage? infoMessage;
            try
            {
                using var client = new HttpClient();
                var cancelSource = new CancellationTokenSource();
                cancelSource.CancelAfter(5 * 1000);
                var responseString = await client.GetStringAsync($"{jsonConfigurationService.Checkers[0]}/service", cancelSource.Token);
                infoMessage = JsonSerializer.Deserialize<CheckerInfoMessage>(responseString, EnoCoreUtil.SerializerOptions);
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
                jsonConfigurationService.Active ?? false,
                jsonConfigurationService.Checkers.Select(x => x.ToString()).ToArray());
        }
    }
}
