namespace EnoEngine
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
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
        public async Task<DateTime> StartNewRound()
        {
            DateTime end;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            this.logger.LogDebug("Starting new Round");
            DateTime begin = DateTime.UtcNow;
            Round newRound;
            Configuration configuration;
            Team[] teams;
            Service[] services;

            // start the next round
            using (var scope = this.serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
                configuration = await db.RetrieveConfiguration();
                double quatherLength = configuration.RoundLengthInSeconds / 4;
                DateTime q2 = begin.AddSeconds(quatherLength);
                DateTime q3 = begin.AddSeconds(quatherLength * 2);
                DateTime q4 = begin.AddSeconds(quatherLength * 3);
                end = begin.AddSeconds(quatherLength * 4);
                newRound = await db.CreateNewRound(begin, q2, q3, q4, end);
                teams = await db.RetrieveActiveTeams();
                services = await db.RetrieveActiveServices();
            }

            this.logger.LogInformation($"CreateNewRound for {newRound.Id} finished ({stopwatch.ElapsedMilliseconds}ms)");

            // insert put tasks
            var insertPutNewFlagsTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertPutFlagsTasks(newRound, teams, services, configuration));
            var insertPutNewNoisesTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertPutNoisesTasks(newRound, teams, services, configuration));
            var insertHavocsTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertHavocsTasks(newRound, teams, services, configuration));

            await insertPutNewFlagsTask;
            await insertPutNewNoisesTask;
            await insertHavocsTask;

            // insert get tasks
            var insertRetrieveCurrentFlagsTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertRetrieveCurrentFlagsTasks(newRound, teams, services, configuration));
            var insertRetrieveOldFlagsTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertRetrieveOldFlagsTasks(newRound, teams, services, configuration));
            var insertGetCurrentNoisesTask = this.databaseUtil.ExecuteScopedDatabaseActionIgnoreErrors(db => db.InsertRetrieveCurrentNoisesTasks(newRound, teams, services, configuration));

            await insertRetrieveCurrentFlagsTask;
            await insertRetrieveOldFlagsTask;
            await insertGetCurrentNoisesTask;

            this.logger.LogInformation($"Checker tasks for round {newRound.Id} created ({stopwatch.ElapsedMilliseconds}ms)");
            await this.HandleRoundEnd(newRound.Id - 1, configuration);
            this.logger.LogInformation($"HandleRoundEnd for round {newRound.Id - 1} finished ({stopwatch.ElapsedMilliseconds}ms)");

            return end;
        }

        public async Task<DateTime> HandleRoundEnd(long roundId, Configuration configuration, bool recalculating = false)
        {
            if (roundId > 0)
            {
                if (!recalculating)
                {
                    await this.RecordServiceStates(roundId);
                }

                await this.CalculateServicePoints(roundId, configuration);
                await this.CalculateTotalPoints();
            }

            await this.GenerateAttackInfo(roundId, configuration);

            var jsonStopWatch = new Stopwatch();
            jsonStopWatch.Start();
            var scoreboard = await this.databaseUtil.RetryScopedDatabaseAction(db => db.GetCurrentScoreboard(roundId));
            var json = JsonSerializer.Serialize(scoreboard, EnoCoreUtil.CamelCaseEnumConverterOptions);
            File.WriteAllText($"{EnoCoreUtil.DataDirectory}scoreboard{roundId}.json", json);
            File.WriteAllText($"{EnoCoreUtil.DataDirectory}scoreboard.json", json);
            jsonStopWatch.Stop();
            this.logger.LogInformation($"Scoreboard Generation Took {jsonStopWatch.ElapsedMilliseconds} ms");
            return DateTime.UtcNow;
        }

        private async Task CalculateTotalPoints()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await this.databaseUtil.RetryScopedDatabaseAction(
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

        private async Task CalculateServicePoints(long roundId, Configuration configuration)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                var (newLatestSnapshotRoundId, oldSnapshotRoundId, services, teams) = await this.databaseUtil.RetryScopedDatabaseAction(
                    db => db.GetPointCalculationFrame(roundId, configuration));

                var servicePointsTasks = new List<Task>(services.Length);
                foreach (var service in services)
                {
                    servicePointsTasks.Add(Task.Run(async () =>
                    {
                        await this.databaseUtil.RetryScopedDatabaseAction(
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

        private async Task GenerateAttackInfo(long roundId, Configuration configuration)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var attackInfo = await this.databaseUtil.RetryScopedDatabaseAction(
                db => db.GetAttackInfo(roundId, configuration.FlagValidityInRounds));
            var json = JsonSerializer.Serialize(attackInfo, EnoCoreUtil.CamelCaseEnumConverterOptions);
            File.WriteAllText($"{EnoCoreUtil.DataDirectory}attack.json", json);
            stopWatch.Stop();
            this.logger.LogInformation($"Attack Info Generation Took {stopWatch.ElapsedMilliseconds} ms");
        }
    }
}
