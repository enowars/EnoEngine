using EnoCore;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoEngine.FlagSubmission;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EnoEngine.Game
{
    interface IFlagSubmissionHandler
    {
        Task<FlagSubmissionResult> HandleFlagSubmission(string flag, string attackerAddressPrefix);
    }

    class CTF : IFlagSubmissionHandler
    {
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoEngine));
        private readonly SemaphoreSlim Lock = new SemaphoreSlim(1);
        private readonly ServiceProvider ServiceProvider;
        private readonly CancellationToken Token;

        public CTF(ServiceProvider serviceProvider, CancellationToken token)
        {
            ServiceProvider = serviceProvider;
            Token = token;
            Task.Run(async () => await new FlagSubmissionEndpoint(this, token).Run());
        }

        public async Task<DateTime> StartNewRound()
        {
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
                long observedRounds = Program.Configuration.CheckedRoundsPerRound > currentRound.Id ? currentRound.Id : Program.Configuration.CheckedRoundsPerRound;

                // start the evaluation
                var handleOldRoundTask = Task.Run(async () => await HandleRoundEnd(oldRound?.Id ?? 0, Program.Configuration));


                // insert put tasks
                var insertPutNewFlagsTask = Task.Run(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertPutFlagsTasks(currentRound.Id, begin, Program.Configuration);
                    }
                });
                var insertPutNewNoisesTask = Task.Run(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertPutNoisesTasks(currentRound, newNoises, Program.Configuration);
                    }
                });
                var insertHavocsTask = Task.Run(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertHavocsTasks(currentRound.Id, begin, Program.Configuration);
                    }
                });

                // give the db some space TODO save the earliest tasks first
                await Task.Delay(1000);

                // insert get tasks
                var insertRetrieveCurrentFlagsTask = Task.Run(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertRetrieveCurrentFlagsTasks(currentRound, newFlags, Program.Configuration);
                    }
                });
                var insertRetrieveOldFlagsTask = Task.Run(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertRetrieveOldFlagsTasks(currentRound, Program.Configuration.CheckedRoundsPerRound - 1, Program.Configuration);
                    }
                });
                var insertGetCurrentNoisesTask = Task.Run(async () =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().InsertRetrieveCurrentNoisesTasks(currentRound, newNoises, Program.Configuration);
                    }
                });

                // TODO start noise for old rounds

                //TODO await in trycatch, we want to wait for everything
                await insertPutNewFlagsTask;
                await insertRetrieveCurrentFlagsTask;
                await insertRetrieveOldFlagsTask;

                await insertPutNewNoisesTask;
                await insertGetCurrentNoisesTask;

                await insertHavocsTask;
                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(CTF),
                    Function = nameof(StartNewRound),
                    RoundId = currentRound.Id,
                    Message = $"All checker tasks for round {currentRound.Id} are created"
                });
                var oldRoundHandlingFinished = await handleOldRoundTask;
                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(CTF),
                    Function = nameof(StartNewRound),
                    RoundId = oldRound?.Id ?? 0,
                    Message = $"Scoreboard calculation for round {oldRound?.Id ?? 0} complete ({(oldRoundHandlingFinished - begin).ToString()})"
                });
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

        public async Task<FlagSubmissionResult> HandleFlagSubmission(string flag, string attackerAddressPrefix)
        { 
            if (!EnoCoreUtils.IsValidFlag(flag))
            {
                return FlagSubmissionResult.Invalid;
            }
            try
            {
                return await ServiceProvider.GetRequiredService<IEnoDatabase>().InsertSubmittedFlag(flag, attackerAddressPrefix, Program.Configuration);
            }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Module = nameof(CTF),
                    Function = nameof(HandleFlagSubmission),
                    Message = $"HandleFlabSubmission() failed: {EnoCoreUtils.FormatException(e)}"
                });
                return FlagSubmissionResult.UnknownError;
            }
        }

        private async Task<DateTime> HandleRoundEnd(long roundId, JsonConfiguration config)
        {
            if (roundId > 0)
            {
                await EnoDatabaseUtils.RecordServiceStates(ServiceProvider, roundId);
                await EnoDatabaseUtils.CalculateAllPoints(ServiceProvider, roundId, config);
            }
            var scoreboard = EnoDatabaseUtils.GetCurrentScoreboard(ServiceProvider, roundId);
            EnoCoreUtils.GenerateCurrentScoreboard(scoreboard, $"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}", roundId);
            return DateTime.UtcNow;
        }
    }
}
