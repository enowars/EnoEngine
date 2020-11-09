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
CancellationTokenSource CancelSource = new CancellationTokenSource();
using var mutex = new Mutex(false, mutexId, out bool _);
try
{
    if (mutex.WaitOne(10, false))
    {
        Configuration configuration;
        if (!File.Exists("ctf.json"))
        {
            Console.WriteLine("Config (ctf.json) does not exist");
            return;
        }

        try
        {
            var content = File.ReadAllText("ctf.json");
            var jsonConfiguration = JsonSerializer.Deserialize<JsonConfiguration>(content);
            if (jsonConfiguration is null)
                throw new Exception("Could not deserialize config.");
            configuration = await jsonConfiguration.ValidateAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load ctf.json: {e.Message}");
            return;
        }

        var serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(configuration)
            .AddSingleton<FlagSubmissionEndpoint>()
            .AddSingleton(new EnoStatistics(nameof(EnoEngine)))
            .AddScoped<IEnoDatabase, EnoDatabase.EnoDatabase>()
            .AddDbContextPool<EnoDatabaseContext>(
                options =>
                {
                    options.UseNpgsql(
                        EnoDatabaseUtils.PostgresConnectionString,
                        pgoptions => pgoptions.EnableRetryOnFailure());
                }, 90)
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
                loggingBuilder.AddConsole();
                loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoFlagSink", CancelSource.Token));
            })
            .BuildServiceProvider(validateScopes: true);
        var submissionEndpoint = serviceProvider.GetRequiredService<FlagSubmissionEndpoint>();
        await submissionEndpoint.Start(configuration, CancelSource.Token);
    }
    else
    {
        Console.WriteLine("Another Instance is already running");
    }
}
finally
{
    mutex?.Close();
}
