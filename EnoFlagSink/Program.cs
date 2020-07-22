using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using EnoCore.Logging;
using EnoCore.Models.Json;
using EnoDatabase;
using EnoEngine.FlagSubmission;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EnoFlagSink
{
    class Program
    {
        private static readonly CancellationTokenSource CancelSource = new CancellationTokenSource();
        public static void Run(string? argument = null)
        {
            JsonConfiguration configuration;
            if (!File.Exists("ctf.json"))
            {
                Console.WriteLine("Config (ctf.json) does not exist");
                return;
            }
            try
            {
                var content = File.ReadAllText("ctf.json");
                configuration = JsonSerializer.Deserialize<JsonConfiguration>(content);
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
                .AddDbContextPool<EnoDatabaseContext>(options =>
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
            var SubmissionEndpoint = serviceProvider.GetRequiredService<FlagSubmissionEndpoint>();
            SubmissionEndpoint.Start(CancelSource.Token, configuration);
        }
        public static void Main(string? argument = null)
        {
            const string mutexId = @"Global\EnoFlagSink";
            using var mutex = new Mutex(false, mutexId, out bool _);
            try
            {
                if (mutex.WaitOne(10, false))
                {
                    Run(argument);
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
        }
    }
}
