using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EnoCore.Logging
{
    public struct SubmissionBatchMessage
    {
        public long FlagsProcessed { get; }
        public long OkFlags { get; }
        public long DuplicateFlags { get; }
        public long OldFlags { get; }
        public long Duration { get; }

        public SubmissionBatchMessage(long flagsProcessed, long okFlags,
            long duplicateFlags, long oldFlags, long duration)
        {
            FlagsProcessed = flagsProcessed;
            OkFlags = okFlags;
            DuplicateFlags = duplicateFlags;
            OldFlags = oldFlags;
            Duration = duration;
        }
    }

    public class EnoStatistics
    {
        private static readonly string PREFIX = "##ENOSTATISTICSMESSAGE ";
        private readonly FileQueue Queue;

        public EnoStatistics(string tool)
        {
            Queue = new FileQueue($"../{tool}", CancellationToken.None);
        }

        public void SubmissionBatchMessage(long flagsProcessed, long okFlags,
            long duplicateFlags, long oldFlags, long duration)
        {
            var message = new SubmissionBatchMessage(flagsProcessed,
                okFlags, duplicateFlags, oldFlags, duration);
            Queue.Enqueue(PREFIX + JsonConvert.ToString(message));
        }
    }
}
