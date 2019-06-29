using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class EnoStatisticMessage
    {
        public FlagsubmissionBatchProcessedMessage FlagsubmissionBatchProcessedMessage { get; set; }
        public ScoreboardJsonGenerationFinishedMessage ScoreboardJsonGenerationFinishedMessage { get; set; }
    }

    public class FlagsubmissionBatchProcessedMessage
    {
        public long FlagsProcessed { get; set; }

        public static EnoStatisticMessage Create(long flagsProcessed)
        {
            return new EnoStatisticMessage()
            {
                FlagsubmissionBatchProcessedMessage = new FlagsubmissionBatchProcessedMessage()
                {
                    FlagsProcessed = flagsProcessed
                }
            };
        }
    }

    public class ScoreboardJsonGenerationFinishedMessage
    {
        public long DurationInMillis { get; set; }

        public static EnoStatisticMessage Create(long duration)
        {
            return new EnoStatisticMessage()
            {
                ScoreboardJsonGenerationFinishedMessage = new ScoreboardJsonGenerationFinishedMessage()
                {
                    DurationInMillis = duration
                }
            };
        }
    }
}
