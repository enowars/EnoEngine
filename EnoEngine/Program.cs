namespace Application
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Configuration;
    using EnoCore.Logging;
    using EnoCore.Models;
    using EnoCore.Scoreboard;
    using EnoDatabase;
    using EnoEngine;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            const string MODE_RECALCULATE = "recalculate";

            const string mutexId = @"Global\EnoEngine";

            CancellationTokenSource cancelSource = new();

            using var mutex = new Mutex(false, mutexId, out bool _);
            var ctfFileName = args.Length == 0 ? "ctf.json" : args[0];

            try
            {
                // Check if another EnoEngine is already running
                if (!mutex.WaitOne(10, false))
                {
                    throw new Exception("Another Instance is already running.");
                }

                // Check if config exists
                if (!File.Exists(ctfFileName))
                {
                    throw new Exception($"Config {ctfFileName} does not exist.");
                }

                // Check if config is valid
                Configuration configuration;
                try
                {
                    string content = File.ReadAllText(ctfFileName);
                    var jsonConfiguration = JsonConfiguration.Deserialize(content);
                    if (jsonConfiguration is null)
                    {
                        throw new Exception("Deserialization of config failed.");
                    }

                    configuration = await jsonConfiguration.ValidateAsync();
                }
                catch (JsonException e)
                {
                    throw new Exception("Configuration could not be deserialized", e);
                }
                catch (JsonConfigurationValidationException e)
                {
                    throw new Exception($"Configuration is invalid.", e);
                }

                // Generate scoreboardInfo.json
                try
                {
                    var teams = configuration.Teams
                        .Select(s => new ScoreboardInfoTeam(s.Id, s.Name, s.LogoUrl, s.CountryFlagUrl))
                        .ToArray();
                    var services = configuration.Services
                        .Select(s => new ScoreboardService(s.Id, s.Name, s.FlagVariants, Array.Empty<ScoreboardFirstBlood>()))
                        .ToArray();
                    var json = JsonSerializer.Serialize(
                        new ScoreboardInfo(configuration.DnsSuffix, services, teams),
                        EnoCoreUtil.CamelCaseEnumConverterOptions);
                    File.WriteAllText($"{EnoCoreUtil.DataDirectory}scoreboardInfo.json", json);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to generate scoreboardInfo.json: {e.Message}");
                }

                // Set up dependency injection tree
                var serviceProvider = new ServiceCollection()
                    .AddLogging()
                    .AddSingleton(configuration)
                    .AddSingleton(typeof(EnoDatabaseUtil))
                    .AddSingleton(new EnoStatistics(nameof(EnoEngine)))
                    .AddScoped<IEnoDatabase, EnoDatabase>()
                    .AddSingleton<EnoEngine>()
                    .AddDbContextPool<EnoDatabaseContext>(
                        options =>
                        {
                            options.UseNpgsql(
                                EnoDatabaseContext.PostgresConnectionString,
                                pgoptions => pgoptions.EnableRetryOnFailure());
                        },
                        90)
                    .AddLogging(loggingBuilder =>
                    {
                        loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                        loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
                        loggingBuilder.AddConsole();
                        loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoEngine", cancelSource.Token));
                    })
                    .BuildServiceProvider(validateScopes: true);

                // Go!
                var engine = serviceProvider.GetRequiredService<EnoEngine>();
                if (args.Length == 1 && args[0] == MODE_RECALCULATE)
                {
                    engine.RunRecalculation().Wait();
                }
                else if (args.Length == 0)
                {
                    engine.RunContest().Wait();
                }
                else
                {
                    throw new Exception("Invalid arguments");
                }
            }
            catch (Exception e)
            {
#if DEBUG
                throw;
#else
    Console.WriteLine(e.Message);
    return 1;
#endif
            }
            finally
            {
                mutex?.Close();
            }

            return 0;
        }
    }
}
