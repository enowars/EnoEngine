using EnoCore;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoEngine.FlagSubmission;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnoEngine.Game
{
    interface IFlagSubmissionHandler
    {
        Task<FlagSubmissionResult> HandleFlagSubmission(string flag, string attackerSubmissionAddress);
    }

    class CTF : IFlagSubmissionHandler
    {
        private static readonly ILogger Logger = EnoCoreUtils.Loggers.CreateLogger<CTF>();
        private readonly SemaphoreSlim Lock = new SemaphoreSlim(1);
        private readonly Random Rnd = new Random();
        private readonly CancellationToken Token;
        private readonly Task FlagSubmissionEndpointTask;
        
        public CTF(CancellationToken token)
        {
            Token = token;
            FlagSubmissionEndpointTask = new FlagSubmissionEndpoint(this, token).Run();
        }

        public async Task StartNewRound()
        {
            await Lock.WaitAsync(Token);
            Logger.LogDebug("Starting new Round");
            double quatherLength = Program.Configuration.RoundLengthInSeconds / 4;
            DateTime begin = DateTime.Now;
            DateTime q2 = begin.AddSeconds(quatherLength);
            DateTime q3 = begin.AddSeconds(quatherLength * 2);
            DateTime q4 = begin.AddSeconds(quatherLength * 3);
            DateTime end = begin.AddSeconds(quatherLength * 4);
            try
            {
                // start the next round
                (var currentRound, var currentFlags, var currentNoises) = EnoDatabase.CreateNewRound(begin, q2, q3, q4, end);
                long observedRounds = Program.Configuration.CheckedRoundsPerRound > currentRound.Id ? currentRound.Id : Program.Configuration.CheckedRoundsPerRound;

                // start the evaluation TODO deferred?
                var handleOldRoundTask = HandleRoundEnd(currentRound.Id -1);
                EnoCoreUtils.GenerateCurrentScoreboard($"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}scoreboard.json");

                // insert put tasks
                var insertPutNewFlagsTask = Task.Run(async () => await InsertPutFlagsTasks(begin, currentFlags));
                var insertPutNewNoisesTask = Task.Run(async () => await InsertPutNoisesTasks(begin, currentNoises));

                // give the db some space TODO save the earliest tasks first
                await Task.Delay(1000);

                // insert get tasks
                var insertRetrieveCurrentFlagsTask = Task.Run(async () => await InsertRetrieveCurrentFlagsTasks(q3, currentFlags));
                var insertRetrieveOldFlagsTask = Task.Run(async () => await InsertRetrieveOldFlagsTasks(currentRound));
                var insertGetCurrentNoisesTask = Task.Run(async () => await InsertRetrieveCurrentNoisesTasks(q3, currentNoises));

                // TODO start noise for old rounds
                // TODO start Havok

                //TODO await in trycatch, we want to wait for everything
                await insertPutNewFlagsTask;
                await insertRetrieveCurrentFlagsTask;
                await insertRetrieveOldFlagsTask;

                await insertPutNewNoisesTask;
                await insertGetCurrentNoisesTask;
                Logger.LogInformation($"Round {currentRound.Id} has started");
                await handleOldRoundTask;
                Logger.LogInformation($"Scoreboard calculation complete");
            }
            catch (Exception e)
            {
                Logger.LogError($"StartNewRound failed: {EnoCoreUtils.FormatException(e)}");
            }
            finally
            {
                Lock.Release();
            }
        }

        public async Task<FlagSubmissionResult> HandleFlagSubmission(string flag, string attackerSubmissionAddress)
        {
            //TODO check signature
            try
            {
                return await EnoDatabase.InsertSubmittedFlag(flag, attackerSubmissionAddress, Program.Configuration.FlagValidityInRounds);
            }
            catch (Exception e)
            {
                Logger.LogError($"HandleFlabSubmission() failed: {EnoCoreUtils.FormatException(e)}");

                return FlagSubmissionResult.UnknownError;
            }
        }

        private async Task InsertPutFlagsTasks(DateTime firstFlagTime, IEnumerable<Flag> currentFlags)
        {
            int maxRunningTime = Program.Configuration.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentFlags.Count();

            var tasks = new List<CheckerTask>(currentFlags.Count());
            foreach (var flag in currentFlags)
            {
                tasks.Add(new CheckerTask()
                {
                    Address = flag.Owner.VulnboxAddress,
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.StringRepresentation,
                    RelatedRoundId = flag.GameRoundId,
                    CurrentRoundId = flag.GameRoundId,
                    StartTime = firstFlagTime,
                    TaskIndex = flag.RoundOffset,
                    TaskType = "putflag",
                    TeamName = flag.Owner.Name,
                    ServiceId = flag.ServiceId,
                    TeamId = flag.OwnerId,
                    ServiceName = flag.Service.Name,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New
                });
                firstFlagTime = firstFlagTime.AddSeconds(timeDiff);
            }
            await EnoDatabase.InsertCheckerTasks(tasks);
        }

        private async Task InsertPutNoisesTasks(DateTime firstFlagTime, IEnumerable<Noise> currentNoises)
        {
            int maxRunningTime = Program.Configuration.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentNoises.Count();

            var tasks = new List<CheckerTask>(currentNoises.Count());
            foreach (var noise in currentNoises)
            {
                tasks.Add(new CheckerTask()
                {
                    Address = noise.Owner.VulnboxAddress,
                    MaxRunningTime = maxRunningTime,
                    Payload = noise.StringRepresentation,
                    RelatedRoundId = noise.GameRoundId,
                    CurrentRoundId = noise.GameRoundId,
                    StartTime = firstFlagTime,
                    TaskIndex = noise.RoundOffset,
                    TaskType = "putnoise",
                    TeamName = noise.Owner.Name,
                    ServiceId = noise.ServiceId,
                    TeamId = noise.OwnerId,
                    ServiceName = noise.Service.Name,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New
                });
                firstFlagTime = firstFlagTime.AddSeconds(timeDiff);
            }
            await EnoDatabase.InsertCheckerTasks(tasks);
        }

        private async Task InsertRetrieveCurrentFlagsTasks(DateTime q3, IEnumerable<Flag> currentFlags)
        {
            int maxRunningTime = Program.Configuration.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double) currentFlags.Count();
            var tasks = new List<CheckerTask>(currentFlags.Count());
            foreach (var flag in currentFlags)
            {
                tasks.Add(new CheckerTask()
                {
                    Address = flag.Owner.VulnboxAddress,
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.StringRepresentation,
                    CurrentRoundId = flag.GameRoundId,
                    RelatedRoundId = flag.GameRoundId,
                    StartTime = q3,
                    TaskIndex = flag.RoundOffset,
                    TaskType = "getflag",
                    TeamName = flag.Owner.Name,
                    TeamId = flag.OwnerId,
                    ServiceName = flag.Service.Name,
                    ServiceId = flag.ServiceId,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New
                });
                q3 = q3.AddSeconds(timeDiff);
            }
            await EnoDatabase.InsertCheckerTasks(tasks);
        }

        private async Task InsertRetrieveCurrentNoisesTasks(DateTime q3, IEnumerable<Noise> currentNoise)
        {
            int maxRunningTime = Program.Configuration.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentNoise.Count();
            var tasks = new List<CheckerTask>(currentNoise.Count());
            foreach (var flag in currentNoise)
            {
                tasks.Add(new CheckerTask()
                {
                    Address = flag.Owner.VulnboxAddress,
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.StringRepresentation,
                    CurrentRoundId = flag.GameRoundId,
                    RelatedRoundId = flag.GameRoundId,
                    StartTime = q3,
                    TaskIndex = flag.RoundOffset,
                    TaskType = "getnoise",
                    TeamName = flag.Owner.Name,
                    TeamId = flag.OwnerId,
                    ServiceName = flag.Service.Name,
                    ServiceId = flag.ServiceId,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New
                });
                q3 = q3.AddSeconds(timeDiff);
            }
            await EnoDatabase.InsertCheckerTasks(tasks);
        }

        private async Task InsertRetrieveOldFlagsTasks(Round currentRound)
        {
            await EnoDatabase.InsertRetrieveOldFlagsTasks(currentRound, Program.Configuration.CheckedRoundsPerRound - 1, Program.Configuration.RoundLengthInSeconds);
        }

        private async Task HandleRoundEnd(long roundId)
        {
            Logger.LogDebug($"Handling end of round {roundId}");
            if (roundId > 0)
            {
                await EnoDatabase.RecordServiceStates(roundId);
                EnoDatabase.CalculatedAllPoints(roundId);
            }
        }
    }
}
