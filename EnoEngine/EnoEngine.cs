using EnoEngine.FlagSubmission;
using EnoCore.Models;
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
using EnoDatabase;

namespace EnoEngine
{
    partial class EnoEngine
    {
        private static readonly CancellationTokenSource EngineCancelSource = new CancellationTokenSource();

        private readonly ILogger Logger;
        private readonly JsonConfiguration Configuration;
        private readonly IServiceProvider ServiceProvider;
        private readonly EnoStatistics Statistics;

        public EnoEngine(ILogger<EnoEngine> logger, JsonConfiguration configuration, IServiceProvider serviceProvider, EnoStatistics enoStatistics, FlagSubmissionEndpoint submissionEndpoint)
        {
            Logger = logger;
            Configuration = configuration;
            ServiceProvider = serviceProvider;
            Statistics = enoStatistics;
            submissionEndpoint.Start(EngineCancelSource.Token, configuration);
        }

        public async Task RunContest()
        {
            // Gracefully shutdown when CTRL+C is invoked
            Console.CancelKeyPress += (s, e) => {
                Logger.LogInformation("Shutting down EnoEngine");
                e.Cancel = true;
                EngineCancelSource.Cancel();
            };
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
                    var end = await StartNewRound();
                    await EnoDatabaseUtils.DelayUntil(end, EngineCancelSource.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Logger.LogError($"GameLoop failed: {EnoDatabaseUtils.FormatException(e)}");
            }
            Logger.LogInformation("GameLoop finished");
        }

        private async Task AwaitOldRound()
        {
            var lastRound = await EnoDatabaseUtils.RetryScopedDatabaseAction(ServiceProvider,
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
            var lastFinishedRound = await EnoDatabaseUtils.RetryScopedDatabaseAction(ServiceProvider,
                async (IEnoDatabase db) => await db.PrepareRecalculation());

            for (int i = 1; i <= lastFinishedRound.Id; i++)
            {
                await HandleRoundEnd(i, true);
            }
        }
    }
}
