using EnoEngine.FlagSubmission;
using EnoEngine.Game;
using EnoCore.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EnoCore;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using EnoCore.Models.Database;
using EnoCore.Logging;

namespace EnoEngine
{
    static class IServiceCollectionExtension
    {
        public static IServiceCollection AddEnoEngine(this IServiceCollection services)
        {
            services
                .AddScoped<IEnoDatabase, EnoDatabase>()
                .AddSingleton<IEnoEngine, EnoEngine>()
                .AddLogging()
                .AddDbContextPool<EnoDatabaseContext>(options =>
                {
                    options.UseNpgsql(
                        EnoCoreUtils.PostgresConnectionString,
                        pgoptions => pgoptions.EnableRetryOnFailure());
                }, 90);
            return services;
        }
    }

    interface IEnoEngine
    {
        Task RunContest();
        Task RunRecalculation();
    }

    class EnoEngine : IEnoEngine
    {
        public static readonly string MODE_RECALCULATE = "recalculate";
        private static readonly CancellationTokenSource EngineCancelSource = new CancellationTokenSource();
        private readonly IServiceProvider ServiceProvider;
        public static JsonConfiguration Configuration { get; set; }

        private readonly CTF EnoGame;
        private readonly ILogger Logger;
        private readonly EnoStatistics Statistics;

        public EnoEngine(ILogger<EnoEngine> logger, IServiceProvider serviceProvider)
        {
            Logger = logger;
            ServiceProvider = serviceProvider;
            Statistics = new EnoStatistics(nameof(EnoEngine));
            EnoGame = new CTF(serviceProvider, logger, Statistics, EngineCancelSource.Token);
        }

        public async Task RunContest()
        {
            // Gracefully shutdown when CTRL+C is invoked
            Console.CancelKeyPress += (s, e) => {
                Logger.LogInformation("Shutting down EnoEngine");
                e.Cancel = true;
                EngineCancelSource.Cancel();
            };
            if (!File.Exists("ctf.json"))
            {
                Logger.LogCritical("Config (ctf.json) didn't exist. Creating sample and exiting");
                CreateConfig();
                return;
            }
            var db = ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<IEnoDatabase>();
            var result = db.ApplyConfig(Configuration);
            Configuration.BuildCheckersDict();
            if (result.Success)
            {
                await GameLoop();
            }
            else
            {
                Logger.LogCritical($"Invalid configuration, exiting ({result.ErrorMessage})");
            }
        }

        internal async Task GameLoop()
        {
            try
            {
                //Check if there is an old round running
                await AwaitOldRound();
                while (!EngineCancelSource.IsCancellationRequested)
                {
                    var end = await EnoGame.StartNewRound();
                    await EnoCoreUtils.DelayUntil(end, EngineCancelSource.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Logger.LogError($"GameLoop failed: {EnoCoreUtils.FormatException(e)}");
            }
            Logger.LogInformation("GameLoop finished");
        }

        private static void CreateConfig()
        {
            using (FileStream fs = File.Open("ctf.json", FileMode.Create))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    using (JsonTextWriter jw = new JsonTextWriter(sw))
                    {
                        jw.Formatting = Formatting.Indented;
                        jw.IndentChar = ' ';
                        jw.Indentation = 4;
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jw, new JsonConfiguration());
                    }
                }
            }
        }

        private async Task AwaitOldRound()
        {
            var lastRound = await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                    async (IEnoDatabase db) => await db.GetLastRound());

            if (lastRound != null)
            {
                var span = lastRound.End.Subtract(DateTime.UtcNow);
                if (span.Seconds > 1)
                {
                    Logger.LogInformation($"Sleeping until old round ends ({lastRound.End})");
                    await Task.Delay(span);
                }
            }
        }

        public async Task RunRecalculation()
        {
            Logger.LogInformation("RunRecalculation()");
            var lastFinishedRound = await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                async (IEnoDatabase db) => await db.PrepareRecalculation());

            for (int i = 1; i <= lastFinishedRound.Id; i++)
            {
                await EnoGame.HandleRoundEnd(i, true);
            }
        }
    }
}