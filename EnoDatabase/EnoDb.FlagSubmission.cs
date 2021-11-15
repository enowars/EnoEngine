namespace EnoDatabase;

public partial class EnoDb
{
    public async Task<FlagSubmissionResult[]> TryProcessSubmissionsBatch(
        FlagSubmissionRequest[] submissions,
        long flagValidityInRounds,
        EnoStatistics statistics)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        var results = new FlagSubmissionResult?[submissions.Length];
        bool empty = true;
        long okFlags = 0;
        long duplicateFlags = 0;
        long oldFlags = 0;
        long currentRoundId;
        try
        {
            currentRoundId = (await this.GetLastRound()).Id;
        }
        catch (Exception e)
        {
            this.logger.LogError($"TryProcessSubmissionsBatch could not get latest round: {e}");
            return Array.ConvertAll(results, x => x ?? FlagSubmissionResult.Error);
        }

        var upsertStatement = new StringBuilder();
        upsertStatement.Append(
@$"INSERT INTO ""{nameof(EnoDbContext.SubmittedFlags)}""
(
""{nameof(SubmittedFlag.FlagServiceId)}"",
""{nameof(SubmittedFlag.FlagRoundId)}"",
""{nameof(SubmittedFlag.FlagOwnerId)}"",
""{nameof(SubmittedFlag.FlagRoundOffset)}"",
""{nameof(SubmittedFlag.AttackerTeamId)}"",
""{nameof(SubmittedFlag.RoundId)}"",
""{nameof(SubmittedFlag.SubmissionsCount)}"",
""{nameof(SubmittedFlag.Timestamp)}""
)
VALUES");

        for (int i = 0; i < submissions.Length; i++)
        {
            var submission = submissions[i];
            if (submission.Flag.RoundId + flagValidityInRounds < currentRoundId)
            {
                results[i] = FlagSubmissionResult.Old;
                oldFlags += 1;
            }
            else
            {
                empty = false;
                upsertStatement.Append($@"
(
{submission.Flag.ServiceId},
{submission.Flag.RoundId},
{submission.Flag.OwnerId},
{submission.Flag.RoundOffset},
{submission.AttackerTeamId},
{currentRoundId},
1,
NOW()
),");
            }
        }

        if (empty)
        {
            // TODO statistics
            return Array.ConvertAll(results, x => x ?? throw new InvalidOperationException());
        }

        upsertStatement.Length--; // Strip the final comma
        upsertStatement.Append($@"
ON CONFLICT
(
""{nameof(SubmittedFlag.FlagServiceId)}"",
""{nameof(SubmittedFlag.FlagRoundId)}"",
""{nameof(SubmittedFlag.FlagOwnerId)}"",
""{nameof(SubmittedFlag.FlagRoundOffset)}"",
""{nameof(SubmittedFlag.AttackerTeamId)}""
)
DO NOTHING
RETURNING
""{nameof(SubmittedFlag.FlagServiceId)}"",
""{nameof(SubmittedFlag.FlagOwnerId)}"",
""{nameof(SubmittedFlag.FlagRoundId)}"",
""{nameof(SubmittedFlag.FlagRoundOffset)}"",
""{nameof(SubmittedFlag.AttackerTeamId)}"",
""{nameof(SubmittedFlag.RoundId)}"",
""{nameof(SubmittedFlag.SubmissionsCount)}"",
""{nameof(SubmittedFlag.Timestamp)}""
;");

        List<SubmittedFlag> insertOrUpdateResults;
        try
        {
            insertOrUpdateResults = await this.context.SubmittedFlags.FromSqlRaw(upsertStatement.ToString()).ToListAsync();
        }
        catch (Exception e)
        {
            // TODO statistics
            this.logger.LogError($"{nameof(this.TryProcessSubmissionsBatch)} failed to execute: {e}");
            return Array.ConvertAll(results, x => x ?? FlagSubmissionResult.Error);
        }

        for (int i = 0; i < submissions.Length; i++)
        {
            if (results[i] == null)
            {
                // The result entry has not been set to FlagSubmissionResult.Old, so it could be present in the upsert results
                var submissionRequest = submissions[i];
                if (insertOrUpdateResults.Count > 0 &&
                    submissionRequest.AttackerTeamId == insertOrUpdateResults[0].AttackerTeamId &&
                    submissionRequest.Flag.OwnerId == insertOrUpdateResults[0].FlagOwnerId &&
                    submissionRequest.Flag.ServiceId == insertOrUpdateResults[0].FlagServiceId &&
                    submissionRequest.Flag.RoundOffset == insertOrUpdateResults[0].FlagRoundOffset &&
                    submissionRequest.Flag.RoundId == insertOrUpdateResults[0].FlagRoundId)
                {
                    results[i] = FlagSubmissionResult.Ok;
                    insertOrUpdateResults.RemoveAt(0);
                    okFlags += 1;
                }
                else
                {
                    // It is not, so it was a duplicate
                    results[i] = FlagSubmissionResult.Duplicate;
                    duplicateFlags += 1;
                }
            }
        }

        stopWatch.Stop();

        statistics.LogSubmissionBatchMessage(
            submissions.Length,
            okFlags,
            duplicateFlags,
            oldFlags,
            stopWatch.ElapsedMilliseconds);

        return Array.ConvertAll(results, x => x ?? FlagSubmissionResult.Error);
    }
}
