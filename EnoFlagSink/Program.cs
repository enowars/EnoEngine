using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.Json;
using EnoDatabase;
using EnoEngine.FlagSubmission;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

const string mutexId = @"Global\EnoFlagSink";

CancellationTokenSource cancelSource = new CancellationTokenSource();

using var mutex = new Mutex(false, mutexId, out bool _);

try
{
    // Check if another EnoFlagSink is already running
    if (!mutex.WaitOne(10, false))
    {
        Console.WriteLine("Another Instance is already running.");
        return;
    }

    // Check if config exists
    if (!File.Exists("ctf.json"))
    {
        Console.WriteLine("Config (ctf.json) does not exist");
        return;
    }

    // Check if config is valid
    Configuration configuration;
    try
    {
        var content = File.ReadAllText("ctf.json");
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

    // Set up dependency injection tree
    var serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddSingleton(configuration)
        .AddSingleton<FlagSubmissionEndpoint>()
        .AddSingleton(new EnoStatistics("EnoFlagSink"))
        .AddScoped<IEnoDatabase, EnoDatabase.EnoDatabase>()
        .AddDbContextPool<EnoDatabaseContext>(
            options =>
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
            loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoFlagSink", cancelSource.Token));
        })
        .BuildServiceProvider(validateScopes: true);

    // Go!
    var submissionEndpoint = serviceProvider.GetRequiredService<FlagSubmissionEndpoint>();
    await submissionEndpoint.Start(configuration, cancelSource.Token);
}
finally
{
    mutex?.Close();
}
