namespace EnoCore.Models.Database;

public enum FlagSubmissionResult
{
    Ok,
    Invalid,
    Duplicate,
    Own,
    Old,
    Error,
}

public static class FlagSubmissionResultExtensions
{
    private static readonly byte[] SubmissionResultOk = Encoding.ASCII.GetBytes(" OK\n");
    private static readonly byte[] SubmissionResultInvalid = Encoding.ASCII.GetBytes(" INV\n");
    private static readonly byte[] SubmissionResultDuplicate = Encoding.ASCII.GetBytes(" DUP\n");
    private static readonly byte[] SubmissionResultOwn = Encoding.ASCII.GetBytes(" OWN\n");
    private static readonly byte[] SubmissionResultOld = Encoding.ASCII.GetBytes(" OLD\n");
    private static readonly byte[] SubmissionResultSpamError = Array.Empty<byte>();

    public static byte[] ToFeedbackBytes(this FlagSubmissionResult fsr)
    {
        return fsr switch
        {
            FlagSubmissionResult.Ok => SubmissionResultOk,
            FlagSubmissionResult.Invalid => SubmissionResultInvalid,
            FlagSubmissionResult.Duplicate => SubmissionResultDuplicate,
            FlagSubmissionResult.Own => SubmissionResultOwn,
            FlagSubmissionResult.Old => SubmissionResultOld,
            FlagSubmissionResult.Error => SubmissionResultSpamError,
            _ => throw new InvalidOperationException(),
        };
    }
}
