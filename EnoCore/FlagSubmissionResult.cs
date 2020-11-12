namespace EnoCore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public enum FlagSubmissionResult
    {
        Ok,
        Invalid,
        Duplicate,
        Own,
        Old,
        UnknownError,
        InvalidSenderError,
        SpamError,
    }

    public static class FlagSubmissionResultExtensions
    {
        public const string SubmissionResultOk = "VALID: Flag accepted!\n";
        public const string SubmissionResultInvalid = "INVALID: You have submitted an invalid string!\n";
        public const string SubmissionResultDuplicate = "RESUBMIT: You have already sent this flag!\n";
        public const string SubmissionResultOwn = "OWNFLAG: This flag belongs to you!\n";
        public const string SubmissionResultOld = "OLD: You have submitted an old flag!\n";
        public const string SubmissionResultUnknownError = "ERROR: An unexpected error occured :(\n";
        public const string SubmissionResultInvalidSenderError = "ILLEGAL: Your IP address does not belong to any team's subnet!\n";
        public const string SubmissionResultSpamError = "SPAM: You should send 1 flag per line!\n";
        public const string SubmissionResultReallyUnknownError = "ERROR: An even more unexpected error occured :(\n";

        public static string ToUserFriendlyString(this FlagSubmissionResult fsr)
        {
            return fsr switch
            {
                FlagSubmissionResult.Ok => SubmissionResultOk,
                FlagSubmissionResult.Invalid => SubmissionResultInvalid,
                FlagSubmissionResult.Duplicate => SubmissionResultDuplicate,
                FlagSubmissionResult.Own => SubmissionResultOwn,
                FlagSubmissionResult.Old => SubmissionResultOld,
                FlagSubmissionResult.UnknownError => SubmissionResultUnknownError,
                FlagSubmissionResult.InvalidSenderError => SubmissionResultInvalidSenderError,
                FlagSubmissionResult.SpamError => SubmissionResultSpamError,
                _ => SubmissionResultReallyUnknownError,
            };
        }
    }
}
