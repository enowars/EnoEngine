using EnoCore;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoEngine.FlagSubmission;
using Microsoft.Extensions.DependencyInjection;
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
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoEngine));
        private readonly SemaphoreSlim Lock = new SemaphoreSlim(1);
        private readonly ServiceProvider ServiceProvider;
        private readonly CancellationToken Token;

        public CTF(ServiceProvider serviceProvider, CancellationToken token)
        {
            ServiceProvider = serviceProvider;
            Token = token;
            var flagSub = new FlagSubmissionEndpoint(serviceProvider, token);
            Task.Run(async () => await flagSub.RunProductionEndpoint());
            Task.Run(async () => await flagSub.RunDebugEndpoint());
        }

        public async Task<DateTime> StartNewRound()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            await Lock.WaitAsync(Token);
            Logger.LogDebug(new EnoLogMessage()
            {
                Module = nameof(CTF),
                Function = nameof(StartNewRound),
                Message = "Starting new Round"
            });
            double quatherLength = Program.Configuration.RoundLengthInSeconds / 4;
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
                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(CTF),
                    Function = nameof(StartNewRound),
                    RoundId = currentRound.Id,
                    Message = $"CreateNewRound for {currentRound.Id} finished ({stopwatch.ElapsedMilliseconds}ms)"
                });

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

                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(CTF),
                    Function = nameof(StartNewRound),
                    RoundId = currentRound.Id,
                    Message = $"All checker tasks for round {currentRound.Id} are created ({stopwatch.ElapsedMilliseconds}ms)"
                });
                await HandleRoundEnd(oldRound?.Id ?? 0, Program.Configuration);
                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(CTF),
                    Function = nameof(StartNewRound),
                    RoundId = oldRound?.Id ?? 0,
                    Message = $"HandleRoundEnd for round {oldRound?.Id ?? 0} finished ({stopwatch.ElapsedMilliseconds}ms)"
                });
                Logger.Log(StartNewRoundFinishedMessage.Create(oldRound?.Id ?? 0, stopwatch.ElapsedMilliseconds));
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()//TODO link to round
                {
                    Module = nameof(CTF),
                    Function = nameof(StartNewRound), 
                    Message = $"StartNewRound failed: {EnoCoreUtils.FormatException(e)}"
                });
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
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertPutFlagsTasks(roundId, begin, Program.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Message = $"InsertPutFlagsTasks failed because: {e}",
                    RoundId = roundId
                });
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
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertPutNoisesTasks(currentRound, newNoises, Program.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Message = $"InsertPutNewNoisesTasks failed because: {e}",
                    RoundId = currentRound.Id
                });
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
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertHavocsTasks(currentRound.Id, begin, Program.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Message = $"InsertHavocsTasks failed because: {e}",
                    RoundId = currentRound.Id
                });
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
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertRetrieveCurrentFlagsTasks(currentRound, newFlags, Program.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Message = $"InsertRetrieveCurrentFlagsTasks failed because: {e}",
                    RoundId = currentRound.Id
                });
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
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertRetrieveOldFlagsTasks(currentRound, Program.Configuration.CheckedRoundsPerRound - 1, Program.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Message = $"InsertRetrieveOldFlagsTasks failed because: {e}",
                    RoundId = currentRound.Id
                });
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
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertRetrieveCurrentNoisesTasks(currentRound, newNoises, Program.Configuration);
                    }
                });
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Message = $"InsertRetrieveCurrentNoisesTasks failed because: {e}",
                    RoundId = currentRound.Id
                });
            }
            stopWatch.Stop();
            Console.WriteLine($"InsertRetrieveCurrentNoisesTasks took {stopWatch.Elapsed.TotalMilliseconds}ms");
        }

        private async Task<DateTime> HandleRoundEnd(long roundId, JsonConfiguration config)
        {
            if (roundId > 0)
            {
                var newStates = await RecordServiceStates(roundId);
                await CalculateServicePoints(roundId, newStates);
                await CalculateTotalPoints(roundId);
            }
            var jsonStopWatch = new Stopwatch();
            jsonStopWatch.Start();
            var scoreboard = EnoDatabaseUtils.GetCurrentScoreboard(ServiceProvider, roundId);
            EnoCoreUtils.GenerateCurrentScoreboard(scoreboard, $"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}", roundId);
            jsonStopWatch.Stop();
            Logger.Log(ScoreboardJsonGenerationFinishedMessage.Create(jsonStopWatch.ElapsedMilliseconds));
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
                Logger.LogError(new EnoLogMessage()
                {
                    Message = $"CalculateTotalPoints failed because: {e}",
                    RoundId = roundId
                });
            }
            finally
            {
                stopWatch.Stop();
                Logger.Log(CalculateTotalPointsFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
            }
        }

        private async Task CalculateServicePoints(long roundId, Dictionary<(long ServiceId, long TeamId), RoundTeamServiceState> newStates)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                var (newLatestSnapshotRoundId, oldSnapshotRoundId, services, teams) = await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                    async(IEnoDatabase db) => await db.GetPointCalculationFrame(Program.Configuration));

                var servicePointsTasks = new List<Task>(services.Length);
                foreach (var service in services)
                {
                    servicePointsTasks.Add(Task.Run(async () =>
                    {
                        await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                            async (IEnoDatabase db) => await db.CalculateServiceStats(teams, newStates, roundId, service, oldSnapshotRoundId, newLatestSnapshotRoundId));
                    }));
                }
                await Task.WhenAll(servicePointsTasks);
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Message = $"CalculateServicePoints failed because: {e}",
                    RoundId = roundId
                });
            }
            finally
            {
                stopWatch.Stop();
                Logger.Log(CalculateServicePointsFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
            }
        }

        private async Task<Dictionary<(long ServiceId, long TeamId), RoundTeamServiceState>> RecordServiceStates(long roundId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                return await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                    async (IEnoDatabase db) => await db.CalculateRoundTeamServiceStates(ServiceProvider, roundId));
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Message = $"RecordServiceStates failed because: {e}",
                    RoundId = roundId
                });
                return new Dictionary<(long ServiceId, long TeamId), RoundTeamServiceState>();
            }
            finally
            {
                stopWatch.Stop();
                Logger.Log(RecordServiceStatesFinishedMessage.Create(roundId, stopWatch.ElapsedMilliseconds));
            }
        }
    }
}
