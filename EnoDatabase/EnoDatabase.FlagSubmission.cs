using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnoDatabase
{
    public partial class EnoDatabase : IEnoDatabase
    {
        public async Task ProcessSubmissionsBatch(List<(Flag flag, long attackerTeamId,
            TaskCompletionSource<FlagSubmissionResult> result)> submissions,
            long flagValidityInRounds, EnoStatistics statistics)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            long okFlags = 0;
            long duplicateFlags = 0;
            long oldFlags = 0;
            var submittedFlagsStatement = new StringBuilder();
            var flagsStatement = new StringBuilder();
            var waitingTasks = new Dictionary<(long, long, long, long, long), TaskCompletionSource<FlagSubmissionResult>>();
            long currentRoundId = await _context.Rounds
                .OrderByDescending(r => r.Id)
                .Select(r => r.Id)
                .FirstAsync();
            submittedFlagsStatement.Append("insert into \"SubmittedFlags\" (\"FlagServiceId\", \"FlagRoundId\", \"FlagOwnerId\", \"FlagRoundOffset\", \"AttackerTeamId\", \"RoundId\", \"SubmissionsCount\")\nvalues ");

            // Filter for duplicates within this batch
            var updates = new Dictionary<(long, long, long, long, long), long>(submissions.Count);
            foreach (var (flag, attackerTeamId, result) in submissions)
            {
                var tResult = result;
                if (flag.RoundId + flagValidityInRounds < currentRoundId)
                {
                    tResult.SetResult(FlagSubmissionResult.Old);
                    oldFlags += 1;
                    continue;
                }
                if (updates.TryGetValue((flag.ServiceId, flag.RoundId, flag.OwnerId, flag.RoundOffset, attackerTeamId), out var entry))
                {
                    tResult.SetResult(FlagSubmissionResult.Duplicate);
                    entry += 1;
                    duplicateFlags += 1;
                }
                else
                {
                    waitingTasks.Add((flag.ServiceId, flag.RoundId, flag.OwnerId, flag.RoundOffset, attackerTeamId), result);
                    updates.Add((flag.ServiceId, flag.RoundId, flag.OwnerId, flag.RoundOffset, attackerTeamId), 1);
                }
            }
            if (updates.Count > 0)
            {
                // Sort the elements to prevent deadlocks from conflicting row lock orderings
                var updatesArray = updates.ToArray();
                Array.Sort(updatesArray, (a, b) =>
                {
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
                    return 0;
                });

                // Build the statements
                foreach (var ((serviceId, roundId, ownerId, roundOffset, attackerTeamId), count) in updatesArray)
                {
                    submittedFlagsStatement.Append($"({serviceId}, {roundId}, {ownerId}, {roundOffset}, {attackerTeamId}, {currentRoundId}, {count}),");
                }
                submittedFlagsStatement.Length--; // Pointers are fun!
                submittedFlagsStatement.Append("\non conflict (\"FlagServiceId\", \"FlagRoundId\", \"FlagOwnerId\", \"FlagRoundOffset\", \"AttackerTeamId\") do update set \"SubmissionsCount\" = \"SubmittedFlags\".\"SubmissionsCount\" + excluded.\"SubmissionsCount\" returning \"FlagServiceId\", \"FlagOwnerId\", \"FlagRoundId\", \"FlagRoundOffset\", \"AttackerTeamId\", \"RoundId\", \"SubmissionsCount\";");

                using var transaction = _context.Database.BeginTransaction();
                try
                {
                    var newSubmissions = await _context.SubmittedFlags.FromSqlRaw(submittedFlagsStatement.ToString()).ToArrayAsync();
                    foreach (var newSubmission in newSubmissions)
                    {
                        var tResult = waitingTasks[(newSubmission.FlagServiceId, newSubmission.FlagRoundId, newSubmission.FlagOwnerId, newSubmission.FlagRoundOffset, newSubmission.AttackerTeamId)];
                        if (newSubmission.SubmissionsCount == 1)
                        {
                            okFlags += 1;
                            tResult.SetResult(FlagSubmissionResult.Ok);
                            flagsStatement.Append($"update \"Flags\" set \"Captures\" = \"Captures\" + 1 where \"ServiceId\" = {newSubmission.FlagServiceId} and \"RoundId\" = {newSubmission.FlagRoundId} and \"OwnerId\" = {newSubmission.FlagOwnerId} and \"RoundOffset\" = {newSubmission.FlagRoundOffset};\n");
                        }
                        else
                        {
                            duplicateFlags += 1;
                            tResult.SetResult(FlagSubmissionResult.Duplicate);
                        }
                    }
                    await _context.Database.ExecuteSqlRawAsync(flagsStatement.ToString());
                    await transaction.CommitAsync();
                }
                catch (Exception e)
                {
                    await transaction.RollbackAsync();
                    throw e;
                }
                stopWatch.Stop();

            }
            statistics.SubmissionBatchMessage(submissions.Count,
                okFlags, duplicateFlags, oldFlags, stopWatch.ElapsedMilliseconds);
        }
    }
}
