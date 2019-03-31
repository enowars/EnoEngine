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
            double quatherLength = Program.Configuration.RoundLengthInSeconds / 4;
            DateTime begin = DateTime.Now;
            DateTime q2 = begin.AddSeconds(quatherLength);
            DateTime q3 = begin.AddSeconds(quatherLength * 2);
            DateTime q4 = begin.AddSeconds(quatherLength * 3);
            DateTime end = begin.AddSeconds(quatherLength * 4);
            try
            {
                // start the next round
                (var currentRound, var currentFlags) = EnoDatabase.CreateNewRound(begin, q2, q3, q4, end);
                EnoCoreUtils.GenerateCurrentScoreboard($"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}scoreboard.json");
                long observedRounds = Program.Configuration.CheckedRoundsPerRound > currentRound.Id ? currentRound.Id : Program.Configuration.CheckedRoundsPerRound;

                // start the evaluation TODO deferred?
                await HandleRoundEnd(currentRound.Id -1);

                // insert checker commands
                var insertDeployNewFlagsTask = Task.Run(async () => await InsertDeployFlagsTasks(begin, currentFlags));
                var insertRetrieveCurrentFlagsTask = Task.Run(async () => await InsertRetrieveCurrentFlagsTasks(q3, currentFlags));
                var insertRetrieveOldFlagsTask = Task.Run(async () => await InsertRetrieveOldFlagsTasks(currentRound));

                // TODO start StoreNoise for current and old rounds
                // TODO start Havok

                await insertDeployNewFlagsTask;
                await insertRetrieveCurrentFlagsTask;
                await insertRetrieveOldFlagsTask;
                Logger.LogInformation($"Round {currentRound.Id} has started");
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
            await Lock.WaitAsync(Token);
            try
            {
                //return EnoEngineDBContext.HandleFlagSubmission(flag, attackerSubmissionAddress, (int) CurrentRoundId, Config.FlagValidityInRounds); TODO
                return FlagSubmissionResult.UnknownError;
            }
            catch (Exception e)
            {
                Console.WriteLine("HandleFlabSubmission() failed: {0}\n{1}", e.Message, e.StackTrace);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner Exception: {0}\n{1}", e.InnerException.Message, e.InnerException.StackTrace);
                }
                return FlagSubmissionResult.UnknownError;
            }
            finally
            {
                Lock.Release();
            }
        }

        private async Task InsertDeployFlagsTasks(DateTime firstFlagTime, IEnumerable<Flag> currentFlags)
        {
            long maxRunningTime = Program.Configuration.RoundLengthInSeconds / 4;
            var timeDiff = maxRunningTime - 5 / currentFlags.Count();
            
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
                    TaskType = "DeployFlag",
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

        private async Task InsertRetrieveCurrentFlagsTasks(DateTime q3, IEnumerable<Flag> currentFlags)
        {
            long maxRunningTime = Program.Configuration.RoundLengthInSeconds / 4;
            var timeDiff = maxRunningTime - 5 / currentFlags.Count();
            
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
                    TaskType = "RetrieveFlag",
                    TeamName = flag.Owner.Name,
                    ServiceName = flag.Service.Name,
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
            if (roundId > 0)
            {
                await EnoDatabase.RecordServiceStates(roundId);
                EnoDatabase.CalculatedAllPoints(roundId);
            }
        }
    }
}
