using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnoCore
{
    public partial class EnoDatabase : IEnoDatabase
    {
        public async Task ProcessSubmissionsBatch(List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> result)> submissions, long flagValidityInRounds)
        {
            var stopWatch = new Stopwatch();
            long submittedFlagsStatementDuration;
            long flagsStatementDuration;
            long commitDuration;
            long okFlags = 0;
            long duplicateFlags = 0;
            long oldFlags = 0;
            var submittedFlagsStatement = new StringBuilder();
            var flagsStatement = new StringBuilder();
            var acceptedSubmissions = new List<TaskCompletionSource<FlagSubmissionResult>>(submissions.Count);
            var acceptedSubmissionsSet = new HashSet<(long, long)>();
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
                    var t = Task.Run(() => tResult.TrySetResult(FlagSubmissionResult.Old));
                    oldFlags += 1;
                    continue;
                }
                if (updates.TryGetValue((flag.ServiceId, flag.RoundId, flag.OwnerId, flag.RoundOffset, attackerTeamId), out var entry))
                {
                    var t = Task.Run(() => tResult.TrySetResult(FlagSubmissionResult.Duplicate));
                    entry += 1;
                    duplicateFlags += 1;
                }
                else
                {
                    updates.Add((flag.ServiceId, flag.RoundId, flag.OwnerId, flag.RoundOffset, attackerTeamId), 1);
                    var t = Task.Run(() => tResult.TrySetResult(FlagSubmissionResult.Ok)); //TODO
                    okFlags += 1;
                }
            }
            if (updates.Count == 0)
                return;

            // Sort the elements to prevent deadlocks from conflicting row lock orderings
            var updatesArray = updates.ToArray();
            Array.Sort(updatesArray, (a,b) =>
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
                flagsStatement.Append($"update \"Flags\" set \"Captures\" = \"Captures\" + 1 where \"ServiceId\" = {serviceId} and \"RoundId\" = {roundId} and \"OwnerId\" = {ownerId} and \"RoundOffset\" = {roundOffset};\n");
            }
            submittedFlagsStatement.Length--; // Pointers are fun!
            submittedFlagsStatement.Append("\non conflict (\"FlagServiceId\", \"FlagRoundId\", \"FlagOwnerId\", \"FlagRoundOffset\", \"AttackerTeamId\") do update set \"SubmissionsCount\" = \"SubmittedFlags\".\"SubmissionsCount\" + excluded.\"SubmissionsCount\" returning \"FlagServiceId\", \"FlagOwnerId\", \"FlagRoundId\", \"FlagRoundOffset\", \"AttackerTeamId\", \"RoundId\", \"SubmissionsCount\";");

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                stopWatch.Restart();
                var newSubmissions = await _context.SubmittedFlags.FromSqlRaw(submittedFlagsStatement.ToString()).ToArrayAsync();
                submittedFlagsStatementDuration = stopWatch.ElapsedMilliseconds;
                stopWatch.Restart();
                await _context.Database.ExecuteSqlRawAsync(flagsStatement.ToString());
                flagsStatementDuration = stopWatch.ElapsedMilliseconds;
                stopWatch.Restart();
                await transaction.CommitAsync();
                commitDuration = stopWatch.ElapsedMilliseconds;
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                throw e;
            }
            EnoLogger.LogStatistics(FlagsubmissionBatchProcessedMessage.Create(submissions.Count,
                okFlags, duplicateFlags, oldFlags, submittedFlagsStatementDuration,
                flagsStatementDuration, commitDuration));
        }
    }
}
