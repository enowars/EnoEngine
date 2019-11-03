using EnoCore;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoEngine.FlagSubmission;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EnoEngine.Game
{
    class CTF
    {
        private readonly ILogger Logger;
        private readonly SemaphoreSlim Lock = new SemaphoreSlim(1);
        private readonly IServiceProvider ServiceProvider;
        private readonly CancellationToken Token;

        public CTF(IServiceProvider serviceProvider, ILogger logger, EnoStatistics statistics, CancellationToken token)
        {
            ServiceProvider = serviceProvider;
            Logger = logger;
            Token = token;
            var flagSub = new FlagSubmissionEndpoint(serviceProvider, logger, statistics, token);
            Task.Run(async () => await flagSub.RunProductionEndpoint());
            Task.Run(async () => await flagSub.RunDebugEndpoint());
        }

        public async Task<DateTime> StartNewRound()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            await Lock.WaitAsync(Token);
            Logger.LogDebug("Starting new Round");
            double quatherLength = EnoEngine.Configuration.RoundLengthInSeconds / 4;
            DateTime begin = DateTime.UtcNow;
            DateTime q2 = begin.AddSeconds(quatherLength);
            DateTime q3 = begin.AddSeconds(quatherLength * 2);
            DateTime q4 = begin.AddSeconds(quatherLength * 3);
            DateTime end = begin.AddSeconds(quatherLength * 4);
            try
            {
                Round oldRound;
                Round currentRound;
                List<Flag> newFlags;
                List<Noise> newNoises;
                List<Havoc> newHavocs;

                // start the next round
                using (var scope = ServiceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                    (oldRound, currentRound, newFlags, newNoises, newHavocs) = await db.CreateNewRound(begin, q2, q3, q4, end);
                }
                Logger.LogInformation($"CreateNewRound for {currentRound.Id} finished ({stopwatch.ElapsedMilliseconds}ms)");

                // insert put tasks
                var insertPutNewFlagsTask = Task.Run(async () => await InsertPutNewFlagsTasks(currentRound.Id, begin));
                var insertPutNewNoisesTask = Task.Run(async () => await InsertPutNewNoisesTasks(currentRound, newNoises));
                var insertHavocsTask = Task.Run(async () => await InsertHavocsTasks(currentRound, begin));

                await insertPutNewFlagsTask;
                await insertPutNewNoisesTask;
                await insertHavocsTask;

                // insert get tasks
                var insertRetrieveCurrentFlagsTask = Task.Run(async () => await InsertRetrieveCurrentFlagsTasks(currentRound, newFlags));
                var insertRetrieveOldFlagsTask = Task.Run(async () => await InsertRetrieveOldFlagsTasks(currentRound));
                var insertGetCurrentNoisesTask = Task.Run(async () => await InsertGetCurrentNoisesTask(currentRound, newNoises));

                await insertRetrieveCurrentFlagsTask;
                await insertRetrieveOldFlagsTask;
                await insertGetCurrentNoisesTask;

                Logger.LogInformation($"All checker tasks for round {currentRound.Id} are created ({stopwatch.ElapsedMilliseconds}ms)");
                await HandleRoundEnd(oldRound?.Id ?? 0);
                Logger.LogInformation($"HandleRoundEnd for round {oldRound?.Id ?? 0} finished ({stopwatch.ElapsedMilliseconds}ms)");
                //TODO EnoLogger.LogStatistics(StartNewRoundFinishedMessage.Create(oldRound?.Id ?? 0, stopwatch.ElapsedMilliseconds));
            }
            catch (Exception e)
            {
                Logger.LogError($"StartNewRound failed: {EnoCoreUtils.FormatException(e)}");
            }
            finally
            {
                Lock.Release();
            }
            return end;
        }

        private async Task InsertPutNewFlagsTasks(long roundId, DateTime begin)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoCoreUtils.RetryDatabaseAction(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertPutFlagsTasks(roundId, begin, EnoEngine.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError($"InsertPutFlagsTasks failed because: {e}");
            }
            stopWatch.Stop();
            Console.WriteLine($"InsertPutFlagsTasks took {stopWatch.Elapsed.TotalMilliseconds}ms");
        }

        private async Task InsertPutNewNoisesTasks(Round currentRound, IEnumerable<Noise> newNoises)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoCoreUtils.RetryDatabaseAction(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertPutNoisesTasks(currentRound, newNoises, EnoEngine.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError($"InsertPutNewNoisesTasks failed because: {e}");
                await Task.Delay(1);
            }
            stopWatch.Stop();
            Console.WriteLine($"InsertPutNewNoisesTasks took {stopWatch.Elapsed.TotalMilliseconds}ms");
        }

        private async Task InsertHavocsTasks(Round currentRound, DateTime begin)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoCoreUtils.RetryDatabaseAction(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertHavocsTasks(currentRound.Id, begin, EnoEngine.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError($"InsertHavocsTasks failed because: {e}");
                await Task.Delay(1);
            }
            stopWatch.Stop();
            Console.WriteLine($"InsertHavocsTasks took {stopWatch.Elapsed.TotalMilliseconds}ms");
        }

        private async Task InsertRetrieveCurrentFlagsTasks(Round currentRound, List<Flag> newFlags)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoCoreUtils.RetryDatabaseAction(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertRetrieveCurrentFlagsTasks(currentRound, newFlags, EnoEngine.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError($"InsertRetrieveCurrentFlagsTasks failed because: {e}");
            }
            stopWatch.Stop();
            Console.WriteLine($"InsertRetrieveCurrentFlagsTasks took {stopWatch.Elapsed.TotalMilliseconds}ms");
        }

        private async Task InsertRetrieveOldFlagsTasks(Round currentRound)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoCoreUtils.RetryDatabaseAction(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertRetrieveOldFlagsTasks(currentRound, EnoEngine.Configuration.CheckedRoundsPerRound - 1, EnoEngine.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError($"InsertRetrieveOldFlagsTasks failed because: {e}");
            }
            stopWatch.Stop();
            Console.WriteLine($"InsertRetrieveOldFlagsTasks took {stopWatch.Elapsed.TotalMilliseconds}ms");
        }

        private async Task InsertGetCurrentNoisesTask(Round currentRound, List<Noise> newNoises)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoCoreUtils.RetryDatabaseAction(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertRetrieveCurrentNoisesTasks(currentRound, newNoises, EnoEngine.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError($"InsertRetrieveCurrentNoisesTasks failed because: {e}");
            }
            stopWatch.Stop();
            Console.WriteLine($"InsertRetrieveCurrentNoisesTasks took {stopWatch.Elapsed.TotalMilliseconds}ms");
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
                await CalculateTotalPoints(roundId);
            }
            var jsonStopWatch = new Stopwatch();
            jsonStopWatch.Start();
            var scoreboard = EnoDatabaseUtils.GetCurrentScoreboard(ServiceProvider, roundId);
            EnoCoreUtils.GenerateCurrentScoreboard(scoreboard, $"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}", roundId);
            jsonStopWatch.Stop();
            //TODO EnoLogger.LogStatistics(ScoreboardJsonGenerationFinishedMessage.Create(jsonStopWatch.ElapsedMilliseconds));
            return DateTime.UtcNow;
        }

        private async Task CalculateTotalPoints(long roundId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                    async (IEnoDatabase db) => await db.CalculateTotalPoints());
            }
            catch (Exception e)
            {
                Logger.LogError($"CalculateTotalPoints failed because: {e}");
            }
            finally
            {
                stopWatch.Stop();
                //TODO EnoLogger.LogStatistics(CalculateTotalPointsFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
            }
        }

        public async Task CalculateServicePoints(long roundId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                var (newLatestSnapshotRoundId, oldSnapshotRoundId, services, teams) = await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                    async(IEnoDatabase db) => await db.GetPointCalculationFrame(roundId, EnoEngine.Configuration));

                var servicePointsTasks = new List<Task>(services.Length);
                foreach (var service in services)
                {
                    servicePointsTasks.Add(Task.Run(async () =>
                    {
                        await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                            async (IEnoDatabase db) => await db.CalculateServiceStats(teams, roundId, service, oldSnapshotRoundId, newLatestSnapshotRoundId));
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
                //TODO EnoLogger.LogStatistics(CalculateServicePointsFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
            }
        }

        private async Task RecordServiceStates(long roundId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                    async (IEnoDatabase db) => await db.CalculateRoundTeamServiceStates(ServiceProvider, roundId));
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
