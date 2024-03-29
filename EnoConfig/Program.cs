﻿namespace EnoConfig;

using System;
using System.Buffers;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnoCore;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.CheckerApi;
using EnoCore.Models.Database;
using EnoCore.Models.JsonConfiguration;
using EnoDatabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class Program
{
    private readonly IServiceProvider serviceProvider;

    public Program(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public static int Main(string[] args)
    {
        var cancelSource = new CancellationTokenSource();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<Program>()
            .AddDbContextPool<EnoDbContext>(
                options =>
                {
                    options.UseNpgsql(
                        EnoDbContext.PostgresConnectionString,
                        pgoptions => pgoptions.EnableRetryOnFailure());
                },
                90)
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
                loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoConfig", cancelSource.Token));
            })
            .BuildServiceProvider(validateScopes: true);

        // Go!
        var program = serviceProvider.GetRequiredService<Program>();

        var rootCommand = new RootCommand();

        var flagInfoCommand = new Command("flaginfo", "Show flag information");
        flagInfoCommand.AddOption(new Option<FileInfo>("--input", () => new FileInfo("ctf.json"), "Path to configuration file"));
        flagInfoCommand.AddArgument(new Argument<string>("flag"));
        flagInfoCommand.Handler = CommandHandler.Create<FileInfo, string>(async (input, flag) => await program.FlagInfo(input, flag));
        rootCommand.AddCommand(flagInfoCommand);

        var migrateDbCommand = new Command("migratedb", "Apply db migrations");
        migrateDbCommand.Handler = CommandHandler.Create(program.MigrateDb);
        rootCommand.AddCommand(migrateDbCommand);

        var applyCommand = new Command("apply", "Apply configuration to database");
        applyCommand.AddOption(new Option<FileInfo>("--input", () => new FileInfo("ctf.json"), "Path to configuration file"));
        applyCommand.AddOption(new Option<int?>("--assume_variants", "Use the given value as the amount of variants instead of contacting the checkers"));
        applyCommand.Handler = CommandHandler.Create<FileInfo, int?>(async (input, assume_variants) => await program.Apply(input, assume_variants));
        rootCommand.AddCommand(applyCommand);

        var debugFlagsCommand = new Command("flags", "Generate flags");
        debugFlagsCommand.AddArgument(new Argument<int>("round"));
        debugFlagsCommand.AddArgument(new Argument<FlagEncoding>("encoding"));
        debugFlagsCommand.AddArgument(new Argument<string>("signing_key"));
        debugFlagsCommand.Handler = CommandHandler.Create<int, FlagEncoding, string>(async (round, encoding, signing_key) => await program.Flags(round, encoding, signing_key));
        rootCommand.AddCommand(debugFlagsCommand);

        var roundWarpCommand = new Command("newround", "Start new round");
        roundWarpCommand.Handler = CommandHandler.Create(program.NewRound);
        rootCommand.AddCommand(roundWarpCommand);

        return rootCommand.InvokeAsync(args).Result;
    }

    public async Task<int> FlagInfo(FileInfo input, string flagString)
    {
        var jsonConfiguration = LoadConfig(input);
        if (jsonConfiguration == null)
        {
            return 1;
        }

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnoDbContext>();
        var flag = Flag.Parse(new ReadOnlySequence<byte>(Encoding.ASCII.GetBytes(flagString)), Encoding.ASCII.GetBytes(jsonConfiguration.FlagSigningKey!), FlagEncoding.Legacy, null);
        if (flag == null)
        {
            Console.Error.WriteLine("Could not parse flag");
            return 1;
        }

        var owner = await dbContext.Teams
            .Where(e => e.Id == flag.OwnerId)
            .SingleAsync();
        var attacks = await dbContext.SubmittedFlags
            .Where(e => e.FlagServiceId == flag.ServiceId)
            .Where(e => e.FlagRoundOffset == flag.RoundOffset)
            .Where(e => e.FlagOwnerId == flag.OwnerId)
            .Where(e => e.FlagRoundId == flag.RoundId)
            .Select(sf => new
            {
                Attacker = dbContext.Teams.Where(e => e.Id == sf.AttackerTeamId).Single(),
                Timestamp = sf.Timestamp,
            })
            .OrderBy(e => e.Timestamp)
            .ToArrayAsync();

        Console.WriteLine($"{flagString} (Owner='{owner.Name}', Id={owner.Id}) has been captured {attacks.Length} times");
        foreach (var attack in attacks)
        {
            Console.WriteLine($"{attack.Timestamp.ToString(EnoCoreUtil.DateTimeFormat)}\t{attack.Attacker.Name} (Id={attack.Attacker.Id})");
        }

        return 0;
    }

    public async Task<int> MigrateDb()
    {
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnoDbContext>();
        await dbContext.Database.MigrateAsync();
        return 0;
    }

    public async Task<int> Apply(FileInfo input, int? assume_variants)
    {
        var jsonConfiguration = LoadConfig(input);
        if (jsonConfiguration == null)
        {
            return 1;
        }

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnoDbContext>();
        await dbContext.Database.MigrateAsync();

        var dbConfiguration = await dbContext.Configurations
            .SingleOrDefaultAsync();

        if (dbConfiguration != null)
        {
            dbConfiguration.Title = jsonConfiguration.Title!;
            dbConfiguration.FlagValidityInRounds = jsonConfiguration.FlagValidityInRounds;
            dbConfiguration.CheckedRoundsPerRound = jsonConfiguration.CheckedRoundsPerRound;
            dbConfiguration.RoundLengthInSeconds = jsonConfiguration.RoundLengthInSeconds;
            dbConfiguration.DnsSuffix = jsonConfiguration.DnsSuffix!;
            dbConfiguration.TeamSubnetBytesLength = jsonConfiguration.TeamSubnetBytesLength;
            dbConfiguration.FlagSigningKey = Encoding.ASCII.GetBytes(jsonConfiguration.FlagSigningKey!);
            dbConfiguration.Encoding = jsonConfiguration.Encoding;
            Console.WriteLine($"Updating configuration {dbConfiguration}");
        }
        else
        {
            var configuration = new Configuration(
                1,
                jsonConfiguration.Title!,
                jsonConfiguration.FlagValidityInRounds,
                jsonConfiguration.CheckedRoundsPerRound,
                jsonConfiguration.RoundLengthInSeconds,
                jsonConfiguration.DnsSuffix!,
                jsonConfiguration.TeamSubnetBytesLength,
                Encoding.ASCII.GetBytes(jsonConfiguration.FlagSigningKey!),
                jsonConfiguration.Encoding);
            Console.WriteLine($"Adding configuration {configuration}");
            dbContext.Add(configuration);
        }

        var dbTeams = dbContext.Teams
            .ToList()
            .ToDictionary(t => t.Id);
        foreach (var jsonConfigurationTeam in jsonConfiguration.Teams!)
        {
            if (jsonConfigurationTeam.Id == 0)
            {
                Console.Error.WriteLine("0 is not a valid team id.");
                return 1;
            }

            if (jsonConfigurationTeam.Name == null)
            {
                Console.Error.WriteLine($"Team {jsonConfigurationTeam.Id} name is null");
                return 1;
            }

            if (jsonConfigurationTeam.TeamSubnet is null)
            {
                Console.Error.WriteLine($"Team subnet must not be null (team {jsonConfigurationTeam.Id}).");
                return 1;
            }

            IPAddress ip;
            try
            {
                ip = IPAddress.Parse(jsonConfigurationTeam.TeamSubnet);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Team subnet is no valid IP address (team {jsonConfigurationTeam.Id}).", e);
                return 1;
            }

            byte[] teamSubnet = new byte[jsonConfiguration.TeamSubnetBytesLength];
            Array.Copy(ip.GetAddressBytes(), teamSubnet, jsonConfiguration.TeamSubnetBytesLength);

            if (dbTeams.TryGetValue(jsonConfigurationTeam.Id, out var dbTeam))
            {
                dbTeam.TeamSubnet = teamSubnet;
                dbTeam.Name = jsonConfigurationTeam.Name;
                dbTeam.Id = jsonConfigurationTeam.Id;
                dbTeam.Active = jsonConfigurationTeam.Active;
                dbTeam.Address = jsonConfigurationTeam.Address;
                dbTeam.CountryCode = jsonConfigurationTeam.CountryCode;
                dbTeam.LogoUrl = jsonConfigurationTeam.LogoUrl;
                dbTeams.Remove(jsonConfigurationTeam.Id);
                Console.WriteLine($"Updating team {dbTeam})");
            }
            else
            {
                var newTeam = new Team()
                {
                    TeamSubnet = teamSubnet,
                    Name = jsonConfigurationTeam.Name,
                    LogoUrl = jsonConfigurationTeam.LogoUrl,
                    CountryCode = jsonConfigurationTeam.CountryCode,
                    Id = jsonConfigurationTeam.Id,
                    Active = jsonConfigurationTeam.Active,
                    Address = jsonConfigurationTeam.Address,
                };
                Console.WriteLine($"Adding team {newTeam})");
                dbContext.Teams.Add(newTeam);
            }
        }

        foreach (var (teamId, team) in dbTeams)
        {
            Console.WriteLine($"Deactivating stale service in db ({teamId})");
            team.Active = false;
        }

        var dbServices = dbContext.Services
            .ToList()
            .ToDictionary(s => s.Id);
        foreach (var jsonConfigurationService in jsonConfiguration.Services!)
        {
            if (jsonConfigurationService.Id == 0)
            {
                Console.Error.WriteLine("Service id must not be 0.");
                return 1;
            }

            if (jsonConfigurationService.Name is null)
            {
                Console.Error.WriteLine($"Service name must not be null (service {jsonConfigurationService.Id}).");
                return 1;
            }

            if (jsonConfigurationService.WeightFactor == 0)
            {
                Console.Error.WriteLine($"Service weight must not be 0 (service {jsonConfigurationService.Id}).");
                return 1;
            }

            if (jsonConfigurationService.Checkers is null)
            {
                Console.Error.WriteLine($"Service checkers must not be null (service {jsonConfigurationService.Id}).");
                return 1;
            }

            if (jsonConfigurationService.Checkers.Length == 0)
            {
                Console.Error.WriteLine($"Service checkers must not be empty (service {jsonConfigurationService.Id}).");
                return 1;
            }

            int flagVariants;
            int noiseVariants;
            int havocVariants;
            if (assume_variants is int stores)
            {
                flagVariants = stores;
                noiseVariants = stores;
                havocVariants = stores;
            }
            else
            {
                try
                {
                    using var client = new HttpClient();
                    var cancelSource = new CancellationTokenSource();
                    cancelSource.CancelAfter(2 * 1000);
                    var responseString = await client.GetStringAsync($"{jsonConfigurationService.Checkers[0]}/service", cancelSource.Token);
                    var infoMessage = JsonSerializer.Deserialize<CheckerInfoMessage>(responseString, EnoCoreUtil.CamelCaseEnumConverterOptions);

                    if (infoMessage == null)
                    {
                        Console.Error.WriteLine($"Service checker failed to respond to info request (service {jsonConfigurationService.Id}).");
                        return 1;
                    }

                    Console.WriteLine($"Service {jsonConfigurationService.Id} checker sent {infoMessage}");
                    flagVariants = infoMessage.FlagVariants;
                    noiseVariants = infoMessage.NoiseVariants;
                    havocVariants = infoMessage.HavocVariants;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Service checker failed to respond to info request (service {jsonConfigurationService.Id}).", e);
                    return 1;
                }
            }

            if (dbServices.TryGetValue(jsonConfigurationService.Id, out var dbService))
            {
                dbService.Name = jsonConfigurationService.Name;
                dbService.FlagsPerRound = flagVariants * jsonConfigurationService.FlagsPerRoundMultiplier;
                dbService.NoisesPerRound = noiseVariants * jsonConfigurationService.NoisesPerRoundMultiplier;
                dbService.HavocsPerRound = havocVariants * jsonConfigurationService.HavocsPerRoundMultiplier;
                dbService.FlagVariants = flagVariants;
                dbService.NoiseVariants = noiseVariants;
                dbService.HavocVariants = havocVariants;
                dbService.Active = jsonConfigurationService.Active;
                dbService.Checkers = jsonConfigurationService.Checkers;
                dbService.WeightFactor = jsonConfigurationService.WeightFactor;
                Console.WriteLine($"Updating service {dbService}");
                dbServices.Remove(dbService.Id);
            }
            else
            {
                var newService = new Service(
                    jsonConfigurationService.Id,
                    jsonConfigurationService.WeightFactor,
                    jsonConfigurationService.Name,
                    jsonConfigurationService.FlagsPerRoundMultiplier * flagVariants,
                    jsonConfigurationService.NoisesPerRoundMultiplier * noiseVariants,
                    jsonConfigurationService.HavocsPerRoundMultiplier * havocVariants,
                    flagVariants,
                    noiseVariants,
                    havocVariants,
                    jsonConfigurationService.Checkers,
                    jsonConfigurationService.Active);
                Console.WriteLine($"Adding service {newService}");
                dbContext.Services.Add(newService);
            }

            foreach (var jsonConfigurationTeam in jsonConfiguration.Teams!)
            {
                if (dbContext.TeamServicePoints
                    .Where(e => e.TeamId == jsonConfigurationTeam.Id)
                    .Where(e => e.ServiceId == jsonConfigurationService.Id)
                    .Count() != 1)
                {
                    dbContext.TeamServicePoints.Add(
                        new TeamServicePoints(
                            jsonConfigurationTeam.Id,
                            jsonConfigurationService.Id,
                            0,
                            0,
                            0,
                            ServiceStatus.OK,
                            null));
                }
            }
        }

        foreach (var (serviceId, service) in dbServices)
        {
            Console.WriteLine($"Deactivating stale service in db ({serviceId})");
            service.Active = false;
        }

        Console.WriteLine("Saving changes...");
        await dbContext.SaveChangesAsync();
        Console.WriteLine("done");
        return 0;
    }

    public async Task<int> Flags(int round, FlagEncoding encoding, string signing_key)
    {
        var key = Encoding.ASCII.GetBytes(signing_key);
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnoDbContext>();

        var teams = await dbContext.Teams.ToArrayAsync();
        var services = await dbContext.Services.ToArrayAsync();

        foreach (var team in teams)
        {
            foreach (var service in services)
            {
                for (int i = 0; i < service.FlagsPerRound; i++)
                {
                    var flag = new Flag(team.Id, service.Id, i, round);
                    Console.WriteLine(flag.ToString(key, encoding));
                }
            }
        }

        return 0;
    }

    public async Task<int> NewRound()
    {
        using var scope = this.serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<EnoDbContext>();
        var lastRound = await dbContext.Rounds
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        Round round;
        if (lastRound != null)
        {
            round = new Round(
                lastRound.Id + 1,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow);
        }
        else
        {
            round = new Round(
                1,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow);
        }

        Console.WriteLine($"Adding round {round}");
        dbContext.Add(round);
        await dbContext.SaveChangesAsync();

        return 0;
    }

    private static JsonConfiguration? LoadConfig(FileInfo input)
    {
        if (!input.Exists)
        {
            Console.Error.WriteLine($"{input.FullName} does not exist or could not be read");
            return null;
        }

        var configString = File.ReadAllText(input.FullName);
        JsonConfiguration? jsonConfiguration;
        try
        {
            jsonConfiguration = JsonSerializer.Deserialize<JsonConfiguration>(
                configString,
                EnoCoreUtil.CamelCaseEnumConverterOptions);

            if (jsonConfiguration is null)
            {
                Console.Error.WriteLine("Deserialization of config failed.");
                return null;
            }
        }
        catch (JsonException e)
        {
            Console.Error.WriteLine($"Configuration could not be deserialized: {e.Message}");
            Console.WriteLine($"{e.Message}\n{e.StackTrace}");
            return null;
        }

        if (jsonConfiguration.Title is null)
        {
            Console.Error.WriteLine("title must not be null.");
            return null;
        }

        if (jsonConfiguration.DnsSuffix is null)
        {
            Console.Error.WriteLine("dnsSuffix must not be null.");
            return null;
        }

        if (jsonConfiguration.FlagSigningKey is null)
        {
            Console.Error.WriteLine("flagSigningKey must not be null.");
            return null;
        }

        if (jsonConfiguration.RoundLengthInSeconds <= 0)
        {
            Console.Error.WriteLine("roundLengthInSeconds must not be <= 0.");
            return null;
        }

        if (jsonConfiguration.CheckedRoundsPerRound <= 0)
        {
            Console.Error.WriteLine("checkedRoundsPerRound must not be <= 0.");
            return null;
        }

        if (jsonConfiguration.FlagValidityInRounds <= 0)
        {
            Console.Error.WriteLine("flagValidityInRounds must not be <= 0.");
            return null;
        }

        if (jsonConfiguration.TeamSubnetBytesLength <= 0)
        {
            Console.Error.WriteLine("teamSubnetBytesLength must not be <= 0.");
            return null;
        }

        if (jsonConfiguration.Teams is null)
        {
            Console.Error.WriteLine("teams must not null.");
            return null;
        }

        if (jsonConfiguration.Services is null)
        {
            Console.Error.WriteLine("services must not null.");
            return null;
        }

        if (jsonConfiguration.Teams.Count == 0)
        {
            Console.Error.WriteLine("teams must not be empty.");
            return null;
        }

        if (jsonConfiguration.Services.Count == 0)
        {
            Console.Error.WriteLine("services must not be empty.");
            return null;
        }

        // Assert every team id is unique
        foreach (var team in jsonConfiguration.Teams)
        {
            var dups = jsonConfiguration.Teams
                .Where(t => t.Id == team.Id)
                .Count();

            if (dups > 1)
            {
                Console.Error.WriteLine($"Duplicate Team {team.Id}.");
                return null;
            }
        }

        // Assert every service id is unique
        foreach (var service in jsonConfiguration.Services)
        {
            var dups = jsonConfiguration.Services
                .Where(t => t.Id == service.Id)
                .Count();

            if (dups > 1)
            {
                Console.Error.WriteLine($"Duplicate Service {service.Id}.");
                return null;
            }
        }

        return jsonConfiguration;
    }
}
