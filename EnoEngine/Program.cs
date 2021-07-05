#pragma warning disable SA1200 // Using directives should be placed correctly
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using EnoCore;
using EnoCore.Configuration;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.JsonConfiguration;
using EnoDatabase;
using EnoEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#pragma warning restore SA1200 // Using directives should be placed correctly

const string MODE_RECALCULATE = "recalculate";

const string mutexId = @"Global\EnoEngine";

CancellationTokenSource cancelSource = new();

using var mutex = new Mutex(false, mutexId, out bool _);

try
{
    // Check if another EnoEngine is already running
    if (!mutex.WaitOne(10, false))
    {
        Console.Error.WriteLine("Another Instance is already running.");
        return 1;
    }

    // Check if config exists
    if (!File.Exists("ctf.json"))
    {
        Console.Error.WriteLine("Config (ctf.json) does not exist.");
        return 1;
    }

    // Check if config is valid
    Configuration configuration;
    try
    {
        string content = File.ReadAllText("ctf.json");
        var jsonConfiguration = JsonSerializer.Deserialize<JsonConfiguration>(content, EnoCoreUtil.CamelCaseEnumConverterOptions);
        if (jsonConfiguration is null)
        {
            Console.WriteLine("Deserialization of config failed.");
            return 1;
        }

        configuration = await Configuration.Validate(jsonConfiguration);
    }
    catch (JsonException e)
    {
        Console.Error.WriteLine($"Configuration could not be deserialized: {e.Message}");
        Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
        return 1;
    }
    catch (JsonConfigurationValidationException e)
    {
        Console.Error.WriteLine($"Configuration is invalid: {e.Message}");
        Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
        return 1;
    }

    // Set up dependency injection tree
    var serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddSingleton(configuration)
        .AddSingleton(typeof(EnoDatabaseUtil))
        .AddSingleton(new EnoStatistics(nameof(EnoEngine)))
        .AddScoped<IEnoDatabase, EnoDatabase.EnoDatabase>()
        .AddSingleton<EnoEngine.EnoEngine>()
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
        return 1;
    }
}
finally
{
    mutex?.Close();
}

return 0;
