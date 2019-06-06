﻿using EnoCore;
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
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace EnoEngine.Game
{
    interface IFlagSubmissionHandler
    {
        Task<FlagSubmissionResult> HandleFlagSubmission(string flag, string attackerAddressPrefix);
    }

    ///
    /// code used from https://devblogs.microsoft.com/pfxteam/getting-random-numbers-in-a-thread-safe-way/
    public static class ThreadSafeRandom
    {
        private static readonly RNGCryptoServiceProvider _global = new RNGCryptoServiceProvider();
        [ThreadStatic]
        private static Random _local;

        public static int Next()
        {
            Random inst = _local;
            if (inst == null)
            {
                byte[] buffer = new byte[4];
                _global.GetBytes(buffer);
                _local = inst = new Random(
                    BitConverter.ToInt32(buffer, 0));
            }
            return inst.Next();
        }

        public static int Next(int n) {
            return Next() % n;
        }
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
                var insertPutNewNoisesTask = Task.Run(async () => await InsertPutNoisesTasks(begin, currentNoises));
                var insertHavoksTask = Task.Run(async () => await EnoDatabase.InsertHavoksTasks(currentRound.Id, begin, Program.Configuration));

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



        private async Task InsertPutNoisesTasks(DateTime firstFlagTime, IEnumerable<Noise> currentNoises)
        {
            int maxRunningTime = Program.Configuration.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentNoises.Count();

            var tasks = new List<CheckerTask>(currentNoises.Count());
            foreach (var noise in currentNoises)
            {
                tasks.Add(new CheckerTask()
                {
                    Address = $"service{noise.ServiceId}.team{noise.OwnerId}.{Program.Configuration.DnsSuffix}",
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
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                    RoundLength = Program.Configuration.RoundLengthInSeconds
                });
                firstFlagTime = firstFlagTime.AddSeconds(timeDiff);
            }
            tasks = Shuffle(tasks).ToList();
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
                    Address = $"service{flag.ServiceId}.team{flag.OwnerId}.{Program.Configuration.DnsSuffix}",
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
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                    RoundLength = Program.Configuration.RoundLengthInSeconds
                });
                q3 = q3.AddSeconds(timeDiff);
            }
            tasks = Shuffle(tasks).ToList();
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
                    Address = $"service{flag.ServiceId}.team{flag.OwnerId}.{Program.Configuration.DnsSuffix}",
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
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                    RoundLength = Program.Configuration.RoundLengthInSeconds
                });
                q3 = q3.AddSeconds(timeDiff);
            }
            tasks = Shuffle(tasks).ToList();
            await EnoDatabase.InsertCheckerTasks(tasks);
        }

        private async Task InsertRetrieveOldFlagsTasks(Round currentRound)
        {
            await EnoDatabase.InsertRetrieveOldFlagsTasks(currentRound, Program.Configuration.CheckedRoundsPerRound - 1, Program.Configuration);
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

        ///
        /// code taken from https://stackoverflow.com/questions/1287567/is-using-random-and-orderby-a-good-shuffle-algorithm/1287572#1287572
        private static IEnumerable<T> Shuffle<T>(IEnumerable<T> source)
        {
            T[] elements = source.ToArray();
            for (int i = elements.Length - 1; i >= 0; i--)
            {
                // Swap element "i" with a random earlier element it (or itself)
                // ... except we don't really need to swap it fully, as we can
                // return it immediately, and afterwards it's irrelevant.
                int swapIndex = ThreadSafeRandom.Next(i + 1);
                yield return elements[swapIndex];
                elements[swapIndex] = elements[i];
            }
        }
    }
}
