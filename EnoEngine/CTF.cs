using EnoCore;
using EnoCore.Models.Json;
using EnoEngine.FlagSubmission;
using System;
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
        private readonly CancellationToken Token;

        public CTF(CancellationToken token)
        {
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
                // start the next round
                (var currentRound, var currentFlags, var currentNoises, var currentHavoks) = await EnoDatabase.CreateNewRound(begin, q2, q3, q4, end);
                long observedRounds = Program.Configuration.CheckedRoundsPerRound > currentRound.Id ? currentRound.Id : Program.Configuration.CheckedRoundsPerRound;

                // start the evaluation
                var handleOldRoundTask = Task.Run(async () => await HandleRoundEnd(currentRound.Id - 1));

                // insert put tasks
                var insertPutNewFlagsTask = Task.Run(async () => await EnoDatabase.InsertPutFlagsTasks(currentRound.Id, begin, Program.Configuration));
                var insertPutNewNoisesTask = Task.Run(async () => await EnoDatabase.InsertPutNoisesTasks(begin, currentNoises, Program.Configuration));
                var insertHavoksTask = Task.Run(async () => await EnoDatabase.InsertHavoksTasks(currentRound.Id, begin, Program.Configuration));

                // give the db some space TODO save the earliest tasks first
                await Task.Delay(1000);

                // insert get tasks
                var insertRetrieveCurrentFlagsTask = Task.Run(async () => await EnoDatabase.InsertRetrieveCurrentFlagsTasks(q3, currentFlags, Program.Configuration));
                var insertRetrieveOldFlagsTask = Task.Run(async () => await EnoDatabase.InsertRetrieveOldFlagsTasks(currentRound, Program.Configuration.CheckedRoundsPerRound - 1, Program.Configuration));
                var insertGetCurrentNoisesTask = Task.Run(async () => await EnoDatabase.InsertRetrieveCurrentNoisesTasks(q3, currentNoises, Program.Configuration));

                // TODO start noise for old rounds

                //TODO await in trycatch, we want to wait for everything
                await insertPutNewFlagsTask;
                await insertRetrieveCurrentFlagsTask;
                await insertRetrieveOldFlagsTask;

                await insertPutNewNoisesTask;
                await insertGetCurrentNoisesTask;

                await insertHavoksTask;
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
                    RoundId = currentRound.Id-1,
                    Message = $"Scoreboard calculation for round {currentRound.Id - 1} complete ({(oldRoundHandlingFinished - begin).ToString()})"
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
                return await EnoDatabase.InsertSubmittedFlag(flag, attackerAddressPrefix, Program.Configuration);
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

        private async Task<DateTime> HandleRoundEnd(long roundId)
        {
            if (roundId > 0)
            {
                await EnoDatabase.RecordServiceStates(roundId);
                await EnoDatabase.CalculatedAllPoints(roundId);
            }
            EnoCoreUtils.GenerateCurrentScoreboard($"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}", roundId);
            return DateTime.UtcNow;
        }
    }
}
