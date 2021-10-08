namespace EnoDatabase
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Logging;
    using EnoCore.Models;
    using EnoCore.Models.Database;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    public partial class EnoDatabase : IEnoDatabase
    {
        public async Task ProcessSubmissionsBatch(
            List<(
                string FlagString,
                Flag Flag,
                long AttackerTeamId,
                ChannelWriter<(string Flag, FlagSubmissionResult Result)> Writer)> submissions,
            long flagValidityInRounds,
            EnoStatistics statistics)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            long okFlags = 0;
            long duplicateFlags = 0;
            long oldFlags = 0;
            var submittedFlagsStatement = new StringBuilder();

            List<(string FlagString,
                ChannelWriter<(string Flag, FlagSubmissionResult Result)> responseChannelWriter)> insertOrUpdateInput = new();
            long currentRoundId = await this.context.Rounds
                .OrderByDescending(r => r.Id)
                .Select(r => r.Id)
                .FirstAsync();
            submittedFlagsStatement.Append($"insert into \"{nameof(EnoDatabaseContext.SubmittedFlags)}\" (\"{nameof(SubmittedFlag.FlagServiceId)}\", \"{nameof(SubmittedFlag.FlagRoundId)}\", \"{nameof(SubmittedFlag.FlagOwnerId)}\", \"{nameof(SubmittedFlag.FlagRoundOffset)}\", \"{nameof(SubmittedFlag.AttackerTeamId)}\", \"{nameof(SubmittedFlag.RoundId)}\", \"{nameof(SubmittedFlag.SubmissionsCount)}\", \"{nameof(SubmittedFlag.Timestamp)}\")\nvalues ");

            // Filter for duplicates within this batch
            var updates = new Dictionary<(long, long, long, long, long), long>(submissions.Count);
            foreach (var (flagString, flag, attackerTeamId, writer) in submissions)
            {
                if (flag.RoundId + flagValidityInRounds < currentRoundId)
                {
                    writer.TrySendOrClose((flagString, FlagSubmissionResult.Old));
                    oldFlags += 1;
                    continue;
                }

                if (flag.RoundId > currentRoundId)
                {
                    this.logger.LogError($"A Future Flag was submitted: Round is {currentRoundId}, Flag's round is {flag.RoundId}");
                }

                if (updates.TryGetValue((flag.ServiceId, flag.RoundId, flag.OwnerId, flag.RoundOffset, attackerTeamId), out var entry))
                {
                    writer.TrySendOrClose((flagString, FlagSubmissionResult.Duplicate));
                    entry += 1;
                    duplicateFlags += 1;
                }
                else
                {
                    insertOrUpdateInput.Add((flagString, writer));
                    updates.Add((flag.ServiceId, flag.RoundId, flag.OwnerId, flag.RoundOffset, attackerTeamId), 1);
                }
            }

            if (updates.Count > 0)
            {
                // Sort the elements to prevent deadlocks from conflicting row lock orderings
                var updatesArray = updates.ToArray();
                Array.Sort(updatesArray, (a, b) =>
                {
#pragma warning disable SA1503 // Braces should not be omitted
                    if (a.Key.Item1 < b.Key.Item1)
                        return 1;

                    if (a.Key.Item1 > b.Key.Item1)
                        return -1;
                    if (a.Key.Item2 < b.Key.Item2)
                        return 1;
                    if (a.Key.Item2 > b.Key.Item2)
                        return -1;
                    if (a.Key.Item3 < b.Key.Item3)
                        return 1;
                    if (a.Key.Item3 > b.Key.Item3)
                        return -1;
                    if (a.Key.Item4 < b.Key.Item4)
                        return 1;
                    if (a.Key.Item4 > b.Key.Item4)
                        return -1;
                    if (a.Key.Item5 < b.Key.Item5)
                        return 1;
                    if (a.Key.Item5 > b.Key.Item5)
                        return -1;
#pragma warning restore SA1503 // Braces should not be omitted
                    return 0;
                });

                // Build the statements
                foreach (var ((serviceId, roundId, ownerId, roundOffset, attackerTeamId), count) in updatesArray)
                {
                    submittedFlagsStatement.Append($"({serviceId}, {roundId}, {ownerId}, {roundOffset}, {attackerTeamId}, {currentRoundId}, {count}, NOW()),");
                }

                submittedFlagsStatement.Length--; // Pointers are fun!
                submittedFlagsStatement.Append($"\non conflict (\"{nameof(SubmittedFlag.FlagServiceId)}\", \"{nameof(SubmittedFlag.FlagRoundId)}\", \"{nameof(SubmittedFlag.FlagOwnerId)}\", \"{nameof(SubmittedFlag.FlagRoundOffset)}\", \"{nameof(SubmittedFlag.AttackerTeamId)}\") do update set \"{nameof(SubmittedFlag.SubmissionsCount)}\" = \"{nameof(EnoDatabaseContext.SubmittedFlags)}\".\"{nameof(SubmittedFlag.SubmissionsCount)}\" + excluded.\"{nameof(SubmittedFlag.SubmissionsCount)}\" returning \"{nameof(SubmittedFlag.FlagServiceId)}\", \"{nameof(SubmittedFlag.FlagOwnerId)}\", \"{nameof(SubmittedFlag.FlagRoundId)}\", \"{nameof(SubmittedFlag.FlagRoundOffset)}\", \"{nameof(SubmittedFlag.AttackerTeamId)}\", \"{nameof(SubmittedFlag.RoundId)}\", \"{nameof(SubmittedFlag.SubmissionsCount)}\", \"{nameof(SubmittedFlag.Timestamp)}\";");

                var insertOrUpdateResults = await this.context.SubmittedFlags.FromSqlRaw(submittedFlagsStatement.ToString()).ToArrayAsync();
                for (int i = 0; i < insertOrUpdateResults.Length; i++)
                {
                    var insertOrUpdateResult = insertOrUpdateResults[i];
                    var submissionInput = insertOrUpdateInput[i];
                    if (insertOrUpdateResult.SubmissionsCount == 1)
                    {
                        okFlags += 1;
                        submissionInput.responseChannelWriter.TrySendOrClose((submissionInput.FlagString, FlagSubmissionResult.Ok));
                    }
                    else
                    {
                        duplicateFlags += 1;
                        submissionInput.responseChannelWriter.TrySendOrClose((submissionInput.FlagString, FlagSubmissionResult.Duplicate));
                    }
                }

                stopWatch.Stop();
            }

            statistics.LogSubmissionBatchMessage(
                submissions.Count,
                okFlags,
                duplicateFlags,
                oldFlags,
                stopWatch.ElapsedMilliseconds);
        }
    }
}
