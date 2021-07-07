#pragma warning disable SA1200 // Using directives should be placed correctly
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnoCore;
using EnoCore.Configuration;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.JsonConfiguration;
using EnoDatabase;
using EnoFlagSink;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#pragma warning restore SA1200 // Using directives should be placed correctly

const string mutexId = @"Global\EnoFlagSink";

CancellationTokenSource cancelSource = new CancellationTokenSource();

using var mutex = new Mutex(false, mutexId, out bool _);

try
{
    // Check if another EnoFlagSink is already running
    if (!mutex.WaitOne(10, false))
    {
        Console.WriteLine("Another Instance is already running.");
        return 1;
    }

    // Check if config exists
    if (!File.Exists("ctf.json"))
    {
        Console.WriteLine("Config (ctf.json) does not exist");
        return 1;
    }

    // Check if config is valid
    Configuration configuration;
    try
    {
        var content = File.ReadAllText("ctf.json");
        var jsonConfiguration = JsonSerializer.Deserialize<JsonConfiguration>(content, EnoCoreUtil.SerializerOptions);
        if (jsonConfiguration is null)
        {
            Console.WriteLine("Deserialization of config failed.");
            return 1;
        }

        configuration = await Configuration.LoadAndValidate(jsonConfiguration);
    }
    catch (JsonException e)
    {
        Console.WriteLine($"Configuration could not be deserialized: {e.Message}");
        Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
        return 1;
    }
    catch (JsonConfigurationValidationException e)
    {
        Console.WriteLine($"Configuration is invalid: {e.Message}");
        Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
        return 1;
    }

    // Set up dependency injection tree
    var serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddSingleton(configuration)
        .AddSingleton(typeof(EnoDatabaseUtil))
        .AddSingleton<FlagSubmissionEndpoint>()
        .AddSingleton(new EnoStatistics("EnoFlagSink"))
        .AddScoped<IEnoDatabase, EnoDatabase.EnoDatabase>()
        .AddDbContextPool<EnoDatabaseContext>(
            options =>
            {
                options.UseNpgsql(EnoDatabaseContext.PostgresConnectionString);
            },
            10)
        .AddLogging(loggingBuilder =>
        {
            loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
            loggingBuilder.AddConsole();
            loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoFlagSink", cancelSource.Token));
        })
        .BuildServiceProvider(validateScopes: true);

    var submissionEndpoint = serviceProvider.GetRequiredService<FlagSubmissionEndpoint>();
    await submissionEndpoint.Start(configuration, cancelSource.Token);
}
finally
{
    mutex?.Close();
}

return 0;
