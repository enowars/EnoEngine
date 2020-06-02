using EnoCore;
using EnoCore.Logging;
using EnoCore.Models.Json;
using EnoEngine.FlagSubmission;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
                Console.WriteLine("Config (ctf.json) didn't exist. Creating sample and exiting");
                CreateConfig();
                return;
            }
            try
            {
                var content = File.ReadAllText("ctf.json");
                configuration = JsonConvert.DeserializeObject<JsonConfiguration>(content);
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
                .AddScoped<IEnoDatabase, EnoDatabase>()
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

        private static void CreateConfig()
        {
            using FileStream fs = File.Open("ctf.json", FileMode.Create);
            using StreamWriter sw = new StreamWriter(fs);
            using JsonTextWriter jw = new JsonTextWriter(sw)
            {
                Formatting = Formatting.Indented,
                IndentChar = ' ',
                Indentation = 4
            };
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(jw, new JsonConfiguration());
        }
    }
}
