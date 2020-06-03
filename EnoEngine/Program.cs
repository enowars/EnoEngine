using EnoCore;
using EnoCore.Logging;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoCore.Utils;
using EnoDatabase;
using EnoEngine.FlagSubmission;
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

namespace EnoEngine
{
    class Program
    {
        private static readonly string MODE_RECALCULATE = "recalculate";
        private static readonly CancellationTokenSource CancelSource = new CancellationTokenSource();

        public static void Main(string? argument = null)
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
                .AddSingleton<EnoEngine>()
                .AddDbContextPool<EnoDatabaseContext>(options =>
                {
                    options.UseNpgsql(
                        EnoCoreUtils.PostgresConnectionString,
                        pgoptions => pgoptions.EnableRetryOnFailure());
                }, 90)
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                    loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
                    loggingBuilder.AddConsole();
                    loggingBuilder.AddProvider(new EnoLogMessageLoggerProvider("EnoEngine", CancelSource.Token));
                })
                .BuildServiceProvider(validateScopes: true);

            var engine = serviceProvider.GetRequiredService<EnoEngine>();
            if (argument == MODE_RECALCULATE)
            {
                engine.RunRecalculation().Wait();
            }
            else
            {
                engine.RunContest().Wait();
            }
        }
    }
}
