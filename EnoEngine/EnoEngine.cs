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
using System.Net.Http;
using System.Text.Json;
using EnoCore.Utils;

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
            await FetchAndApplyCheckersInfo(Configuration);
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

        private async Task GetCheckerInfo (JsonConfigurationService s)
        {
            var Client = new HttpClient();
            try
            {
                var cancelSource = new CancellationTokenSource();
                cancelSource.CancelAfter(10 * 1000);
                var response = await Client.GetAsync($"{s.Checkers[0]}/service", cancelSource.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var responseString = (await response.Content.ReadAsStringAsync()).TrimEnd(Environment.NewLine.ToCharArray());
                    Logger.LogDebug($"GetCheckerInfo for Service {s.Name} received: \"{responseString}\"");
                    var resultMessage = JsonSerializer.Deserialize<CheckerInfoMessage>(responseString);
                    s.FetchedFlagsPerRound = resultMessage.FlagCount;
                    s.FetchedNoisesPerRound = resultMessage.NoiseCount;
                    s.FetchedHavocsPerRound = resultMessage.HavocCount;
                    if (s.FlagsPerRound == 0 && s.NoisesPerRound == 0 && s.HavocsPerRound == 0)
                    {
                        Logger.LogDebug($"GetCheckerInfo for Service {s.Name} setting inactive");
                        s.Active = false;
                    }
                }
                else
                    Logger.LogError($"GetCheckerInfo: Service {s.Name} returned Status Code {response.StatusCode}");
            }
            catch (Exception e)
            {
                Logger.LogError($"GetCheckerInfo Failed for Service {s.Name}");
                Logger.LogError(e.ToFancyString());
                s.Active = false;
                return;
            }
        }
        private async Task FetchAndApplyCheckersInfo(JsonConfiguration config)
        {
            List<Task> AllTasks = new List<Task>();
            foreach (var s in config.Services)
            {
                Logger.LogDebug($"GetCheckerInfo: Fetching Information for {s.Name}");
                AllTasks.Add(GetCheckerInfo(s));
            }
            await Task.WhenAll(AllTasks);
        }

        private async Task GameLoop()
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
