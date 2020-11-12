using EnoCore;
using EnoCore.Logging;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoDatabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using EnoEngine;
using EnoCore.Models;
using EnoCore.JsonConfiguration;

const string MODE_RECALCULATE = "recalculate";
const string mutexId = @"Global\EnoEngine";

CancellationTokenSource cancelSource = new();
using var mutex = new Mutex(false, mutexId, out bool _);

try
{
    // Check if another EnoEngine is already running
    if (!mutex.WaitOne(10, false))
    {
        Console.WriteLine("Another Instance is already running.");
        return;
    }

    // Check if config exists
    if (!File.Exists("ctf.json"))
    {
        Console.WriteLine("Config (ctf.json) does not exist.");
        return;
    }

    // Check if config is valid
    Configuration configuration;
    try
    {
        string content = File.ReadAllText("ctf.json");
        var jsonConfiguration = JsonConfiguration.Deserialize(content);
        if (jsonConfiguration is null)
        {
            Console.WriteLine("Deserialization of config failed.");
            return;
        }
        configuration = await jsonConfiguration.ValidateAsync();
    }
    catch (JsonException e)
    {
        Console.WriteLine($"Configuration could not be deserialized: {e.Message}");
        return;
    }
    catch (JsonConfigurationValidationException e)
    {
        Console.WriteLine($"Configuration is invalid: {e.Message}");
        return;
    }

    // Generate round 0 scoreboard.json
    try
    {
        var json = JsonSerializer.Serialize(new EnoEngineScoreboardInfo(configuration));
        File.WriteAllText($"{EnoDataDirectory.Directory}scoreboardInfo.json", json);
    }
    catch (Exception e)
    {
        Console.WriteLine($"Failed to generate scoreboardInfo.json: {e.Message}");
    }

    // Set up dependency injection tree
    var serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddSingleton(configuration)
        .AddSingleton(new EnoStatistics(nameof(EnoEngine)))
        .AddScoped<IEnoDatabase, EnoDatabase.EnoDatabase>()
        .AddSingleton<EnoEngine.EnoEngine>()
        .AddDbContextPool<EnoDatabaseContext>(options =>
        {
            options.UseNpgsql(
                EnoDatabaseContext.PostgresConnectionString,
                pgoptions => pgoptions.EnableRetryOnFailure());
        }, 90)
        .AddLogging(loggingBuilder =>
        {
            loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
            loggingBuilder.AddConsole();
            loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoEngine", cancelSource.Token));
        })
        .BuildServiceProvider(validateScopes: true);

    // Go!
    var engine = serviceProvider.GetRequiredService<EnoEngine.EnoEngine>();
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
        Console.WriteLine("Invalid arguments");
        return;
    }
}
finally
{
    mutex?.Close();
}
