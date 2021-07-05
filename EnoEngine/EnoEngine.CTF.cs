namespace EnoEngine
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Models;
    using EnoCore.Models.Database;
    using EnoDatabase;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    internal partial class EnoEngine
    {
        private static readonly HttpClient Client = new HttpClient();

        public async Task<DateTime> StartNewRound()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            this.logger.LogDebug("Starting new Round");
            double quatherLength = this.configuration.RoundLengthInSeconds / 4;
            DateTime begin = DateTime.UtcNow;
            DateTime q2 = begin.AddSeconds(quatherLength);
            DateTime q3 = begin.AddSeconds(quatherLength * 2);
            DateTime q4 = begin.AddSeconds(quatherLength * 3);
            DateTime end = begin.AddSeconds(quatherLength * 4);
            try
            {
                Round newRound;

                // start the next round
                using (var scope = this.serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                    newRound = await db.CreateNewRound(begin, q2, q3, q4, end);
                }

                this.logger.LogInformation($"CreateNewRound for {newRound.Id} finished ({stopwatch.ElapsedMilliseconds}ms)");

                // insert put tasks
                var insertPutNewFlagsTask = this.databaseUtil.RetryScopedDatabaseAction(this.serviceProvider, db => db.InsertPutFlagsTasks(newRound, this.configuration));
                var insertPutNewNoisesTask = this.databaseUtil.RetryScopedDatabaseAction(this.serviceProvider, db => db.InsertPutNoisesTasks(newRound, this.configuration));
                var insertHavocsTask = this.databaseUtil.RetryScopedDatabaseAction(this.serviceProvider, db => db.InsertHavocsTasks(newRound, this.configuration));

                await insertPutNewFlagsTask;
                await insertPutNewNoisesTask;
                await insertHavocsTask;

                // insert get tasks
                var insertRetrieveCurrentFlagsTask = this.databaseUtil.RetryScopedDatabaseAction(this.serviceProvider, db => db.InsertRetrieveCurrentFlagsTasks(newRound, this.configuration));
                var insertRetrieveOldFlagsTask = this.databaseUtil.RetryScopedDatabaseAction(this.serviceProvider, db => db.InsertRetrieveOldFlagsTasks(newRound, this.configuration));
                var insertGetCurrentNoisesTask = this.databaseUtil.RetryScopedDatabaseAction(this.serviceProvider, db => db.InsertRetrieveCurrentNoisesTasks(newRound, this.configuration));

                await insertRetrieveCurrentFlagsTask;
                await insertRetrieveOldFlagsTask;
                await insertGetCurrentNoisesTask;

                this.logger.LogInformation($"All checker tasks for round {newRound.Id} are created ({stopwatch.ElapsedMilliseconds}ms)");
                await this.HandleRoundEnd(newRound.Id - 1);
                this.logger.LogInformation($"HandleRoundEnd for round {newRound.Id - 1} finished ({stopwatch.ElapsedMilliseconds}ms)");

                // TODO EnoLogger.LogStatistics(StartNewRoundFinishedMessage.Create(oldRound?.Id ?? 0, stopwatch.ElapsedMilliseconds));
            }
            catch (Exception e)
            {
                this.logger.LogError($"StartNewRound failed: {e.ToFancyStringWithCaller()}");
            }

            return end;
        }

        public async Task<DateTime> HandleRoundEnd(long roundId, bool recalculating = false)
        {
            if (roundId > 0)
            {
                if (!recalculating)
                {
                    await this.RecordServiceStates(roundId);
                }

                await this.CalculateServicePoints(roundId);
                await this.CalculateTotalPoints();
            }

            await this.GenerateAttackInfo(roundId);

            var jsonStopWatch = new Stopwatch();
            jsonStopWatch.Start();
            var scoreboard = await this.databaseUtil.RetryScopedDatabaseAction(
                this.serviceProvider,
                db => db.GetCurrentScoreboard(roundId));
            var json = JsonSerializer.Serialize(scoreboard, EnoCoreUtil.CamelCaseEnumConverterOptions);
            File.WriteAllText($"{EnoCoreUtil.DataDirectory}scoreboard{roundId}.json", json);
            File.WriteAllText($"{EnoCoreUtil.DataDirectory}scoreboard.json", json);

            jsonStopWatch.Stop();
            this.logger.LogInformation($"Scoreboard Generation Took {jsonStopWatch.ElapsedMilliseconds} ms");
            try
            {
                var url = Environment.GetEnvironmentVariable("SCOREBOARD_ENDPOINT") ?? "http://localhost:5000/api/scoreboardinfo/scoreboard?adminSecret=secret";
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await Client.PostAsync(url, content);
                this.logger.LogInformation("EnoLandingPage returned:" + response.StatusCode + "\n" + await response.Content.ReadAsStringAsync());
            }
            catch (Exception e)
            {
                this.logger.LogError($"HTTP POST to Scoreboard failed because: {e}");
            }
            return DateTime.UtcNow;
        }

        private async Task CalculateTotalPoints()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await this.databaseUtil.RetryScopedDatabaseAction(
                    this.serviceProvider,
                    db => db.CalculateTotalPoints());
            }
            catch (Exception e)
            {
                this.logger.LogError($"CalculateTotalPoints failed because: {e}");
            }
            finally
            {
                stopWatch.Stop();
                this.logger.LogInformation($"{nameof(this.CalculateTotalPoints)} Took {stopWatch.ElapsedMilliseconds} ms");

                // TODO EnoLogger.LogStatistics(CalculateTotalPointsFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
            }
        }

        private async Task CalculateServicePoints(long roundId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                var (newLatestSnapshotRoundId, oldSnapshotRoundId, services, teams) = await this.databaseUtil.RetryScopedDatabaseAction(
                    this.serviceProvider,
                    db => db.GetPointCalculationFrame(roundId, this.configuration));

                var servicePointsTasks = new List<Task>(services.Length);
                foreach (var service in services)
                {
                    servicePointsTasks.Add(Task.Run(async () =>
                    {
                        await this.databaseUtil.RetryScopedDatabaseAction(
                            this.serviceProvider,
                            db => db.CalculateTeamServicePoints(teams, roundId, service, oldSnapshotRoundId, newLatestSnapshotRoundId));
                    }));
                }

                await Task.WhenAll(servicePointsTasks);
            }
            catch (Exception e)
            {
                this.logger.LogError($"CalculateServicePoints failed because: {e}");
            }
            finally
            {
                stopWatch.Stop();
                this.logger.LogInformation($"{nameof(this.CalculateServicePoints)} Took {stopWatch.ElapsedMilliseconds} ms");

                // TODO EnoLogger.LogStatistics(CalculateServicePointsFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
            }
        }

        private async Task RecordServiceStates(long roundId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await this.databaseUtil.RetryScopedDatabaseAction(
                    this.serviceProvider,
                    db => db.CalculateRoundTeamServiceStates(this.serviceProvider, roundId, this.statistics));
            }
            catch (Exception e)
            {
                this.logger.LogError($"RecordServiceStates failed because: {e}");
            }
            finally
            {
                stopWatch.Stop();

                // TODO EnoLogger.LogStatistics(RecordServiceStatesFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
                this.logger.LogInformation($"RecordServiceStates took {stopWatch.ElapsedMilliseconds}ms");
            }
        }

        private async Task GenerateAttackInfo(long roundId)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var attackInfo = await this.databaseUtil.RetryScopedDatabaseAction(
                this.serviceProvider,
                db => db.GetAttackInfo(roundId, this.configuration));
            var json = JsonSerializer.Serialize(attackInfo, EnoCoreUtil.CamelCaseEnumConverterOptions);
            File.WriteAllText($"{EnoCoreUtil.DataDirectory}attack.json", json);
            stopWatch.Stop();
            this.logger.LogInformation($"Attack Info Generation Took {stopWatch.ElapsedMilliseconds} ms");
        }
    }
}
