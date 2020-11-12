using EnoCore;
using EnoCore.Models;
using EnoDatabase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EnoEngine
{
    partial class EnoEngine
    {
        public async Task<DateTime> StartNewRound()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Logger.LogDebug("Starting new Round");
            double quatherLength = Configuration.RoundLengthInSeconds / 4;
            DateTime begin = DateTime.UtcNow;
            DateTime q2 = begin.AddSeconds(quatherLength);
            DateTime q3 = begin.AddSeconds(quatherLength * 2);
            DateTime q4 = begin.AddSeconds(quatherLength * 3);
            DateTime end = begin.AddSeconds(quatherLength * 4);
            try
            {
                Round newRound;

                // start the next round
                using (var scope = ServiceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                    newRound = await db.CreateNewRound(begin, q2, q3, q4, end);
                }
                Logger.LogInformation($"CreateNewRound for {newRound.Id} finished ({stopwatch.ElapsedMilliseconds}ms)");

                // insert put tasks
                var insertPutNewFlagsTask = EnoDatabaseUtil.RetryScopedDatabaseAction(ServiceProvider, db => db.InsertPutFlagsTasks(newRound, Configuration));
                var insertPutNewNoisesTask = EnoDatabaseUtil.RetryScopedDatabaseAction(ServiceProvider, db => db.InsertPutNoisesTasks(newRound, Configuration));
                var insertHavocsTask = EnoDatabaseUtil.RetryScopedDatabaseAction(ServiceProvider, db => db.InsertHavocsTasks(newRound, Configuration));

                await insertPutNewFlagsTask;
                await insertPutNewNoisesTask;
                await insertHavocsTask;

                // insert get tasks
                var insertRetrieveCurrentFlagsTask = EnoDatabaseUtil.RetryScopedDatabaseAction(ServiceProvider, db => db.InsertRetrieveCurrentFlagsTasks(newRound, Configuration));
                var insertRetrieveOldFlagsTask = EnoDatabaseUtil.RetryScopedDatabaseAction(ServiceProvider, db => db.InsertRetrieveOldFlagsTasks(newRound, Configuration));
                var insertGetCurrentNoisesTask = EnoDatabaseUtil.RetryScopedDatabaseAction(ServiceProvider, db => db.InsertRetrieveCurrentNoisesTasks(newRound, Configuration));

                await insertRetrieveCurrentFlagsTask;
                await insertRetrieveOldFlagsTask;
                await insertGetCurrentNoisesTask;

                Logger.LogInformation($"All checker tasks for round {newRound.Id} are created ({stopwatch.ElapsedMilliseconds}ms)");
                await HandleRoundEnd(newRound.Id - 1);
                Logger.LogInformation($"HandleRoundEnd for round {newRound.Id - 1} finished ({stopwatch.ElapsedMilliseconds}ms)");
                //TODO EnoLogger.LogStatistics(StartNewRoundFinishedMessage.Create(oldRound?.Id ?? 0, stopwatch.ElapsedMilliseconds));
            }
            catch (Exception e)
            {
                Logger.LogError($"StartNewRound failed: {e.ToFancyString()}");
            }
            return end;
        }

        public async Task<DateTime> HandleRoundEnd(long roundId, bool recalculating = false)
        {
            if (roundId > 0)
            {
                if (!recalculating)
                {
                    await RecordServiceStates(roundId);
                }
                await CalculateServicePoints(roundId);
                await CalculateTotalPoints();
            }
            var jsonStopWatch = new Stopwatch();
            jsonStopWatch.Start();
            var scoreboard = await EnoDatabaseUtil.RetryScopedDatabaseAction(
                ServiceProvider,
                db => db.GetCurrentScoreboard(roundId));
            var json = JsonSerializer.Serialize(scoreboard, EnoCoreUtil.CamelCaseEnumConverterOptions);
            File.WriteAllText($"{EnoCoreUtil.DataDirectory}scoreboard{roundId}.json", json);
            File.WriteAllText($"{EnoCoreUtil.DataDirectory}scoreboard.json", json);
            jsonStopWatch.Stop();
            Logger.LogInformation($"Scoreboard Generation Took {jsonStopWatch.ElapsedMilliseconds} ms");
            return DateTime.UtcNow;
        }

        private async Task CalculateTotalPoints()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoDatabaseUtil.RetryScopedDatabaseAction(
                    ServiceProvider,
                    db => db.CalculateTotalPoints());
            }
            catch (Exception e)
            {
                Logger.LogError($"CalculateTotalPoints failed because: {e}");
            }
            finally
            {
                stopWatch.Stop();
                Logger.LogInformation($"{nameof(CalculateTotalPoints)} Took {stopWatch.ElapsedMilliseconds} ms");
                //TODO EnoLogger.LogStatistics(CalculateTotalPointsFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
            }
        }

        public async Task CalculateServicePoints(long roundId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                var (newLatestSnapshotRoundId, oldSnapshotRoundId, services, teams) = await EnoDatabaseUtil.RetryScopedDatabaseAction(ServiceProvider,
                    async (IEnoDatabase db) => await db.GetPointCalculationFrame(roundId, Configuration));

                var servicePointsTasks = new List<Task>(services.Length);
                foreach (var service in services)
                {
                    servicePointsTasks.Add(Task.Run(async () =>
                    {
                        await EnoDatabaseUtil.RetryScopedDatabaseAction(
                            ServiceProvider,
                            db => db.CalculateTeamServicePoints(teams, roundId, service, oldSnapshotRoundId, newLatestSnapshotRoundId));
                    }));
                }
                await Task.WhenAll(servicePointsTasks);
            }
            catch (Exception e)
            {
                Logger.LogError($"CalculateServicePoints failed because: {e}");
            }
            finally
            {
                stopWatch.Stop();
                Logger.LogInformation($"{nameof(CalculateServicePoints)} Took {stopWatch.ElapsedMilliseconds} ms");
                //TODO EnoLogger.LogStatistics(CalculateServicePointsFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
            }
        }

        private async Task RecordServiceStates(long roundId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoDatabaseUtil.RetryScopedDatabaseAction(
                    ServiceProvider,
                    db => db.CalculateRoundTeamServiceStates(ServiceProvider, roundId, Statistics));
            }
            catch (Exception e)
            {
                Logger.LogError($"RecordServiceStates failed because: {e}");
            }
            finally
            {
                stopWatch.Stop();
                //TODO EnoLogger.LogStatistics(RecordServiceStatesFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
                Logger.LogInformation($"RecordServiceStates took {stopWatch.ElapsedMilliseconds}ms");
            }
        }
    }
}
