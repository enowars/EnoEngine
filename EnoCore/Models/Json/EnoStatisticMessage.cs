using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class EnoStatisticMessage
    {
        [JsonProperty("tool")]
        public string Tool { get; set; }
        // Flag Submission
        public FlagsubmissionBatchProcessedMessage FlagsubmissionBatchProcessedMessage { get; set; }
        public FlagsubmissionQueueSizeMessage FlagsubmissionQueueSize { get; set; }
        // Old Round
        public RecordServiceStatesFinishedMessage RecordServiceStatesFinishedMessage { get; set; }
        public CalculateServiceStatsFetchFinishedMessage CalculateServiceStatsFetchFinishedMessage { get; set; }
        public CalculateServicePointsFinishedMessage CalculateServicePointsFinishedMessage { get; set; }
        public CalculateTotalPointsFinishedMessage CalculateTotalPointsFinishedMessage { get; set; }
        public ScoreboardJsonGenerationFinishedMessage ScoreboardJsonGenerationFinishedMessage { get; set; }
        // New Round
        public StartNewRoundFinishedMessage StartNewRoundFinishedMessage { get; set; }
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

    public class FlagsubmissionQueueSizeMessage
    {
        public long QueueSize { get; set; }

        public static EnoStatisticMessage Create(long size)
        {
            return new EnoStatisticMessage()
            {
                FlagsubmissionQueueSize = new FlagsubmissionQueueSizeMessage()
                {
                    QueueSize = size
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

    public class RecordServiceStatesFinishedMessage
    {
        [JsonProperty("round")]
        public long RoundId { get; set; }
        public long DurationInMillis { get; set; }
        public static EnoStatisticMessage Create(long roundId, long duration)
        {
            return new EnoStatisticMessage()
            {
                RecordServiceStatesFinishedMessage = new RecordServiceStatesFinishedMessage()
                {
                    DurationInMillis = duration,
                    RoundId = roundId
                }
            };
        }
    }

    public class CalculateTotalPointsFinishedMessage
    {
        [JsonProperty("round")]
        public long RoundId { get; set; }
        public long DurationInMillis { get; set; }
        public static EnoStatisticMessage Create(long roundId, long duration)
        {
            return new EnoStatisticMessage()
            {
                CalculateTotalPointsFinishedMessage = new CalculateTotalPointsFinishedMessage()
                {
                    DurationInMillis = duration,
                    RoundId = roundId
                }
            };
        }
    }

    public class CalculateServicePointsFinishedMessage
    {
        [JsonProperty("round")]
        public long RoundId { get; set; }
        public long DurationInMillis { get; set; }
        public static EnoStatisticMessage Create(long roundId, long duration)
        {
            return new EnoStatisticMessage()
            {
                CalculateServicePointsFinishedMessage = new CalculateServicePointsFinishedMessage()
                {
                    DurationInMillis = duration,
                    RoundId = roundId
                }
            };
        }
    }

    public class StartNewRoundFinishedMessage
    {
        [JsonProperty("round")]
        public long RoundId { get; set; }
        public long DurationInMillis { get; set; }
        public static EnoStatisticMessage Create(long roundId, long duration)
        {
            return new EnoStatisticMessage()
            {
                StartNewRoundFinishedMessage = new StartNewRoundFinishedMessage()
                {
                    DurationInMillis = duration,
                    RoundId = roundId
                }
            };
        }
    }

    public class CalculateServiceStatsFetchFinishedMessage
    {
        [JsonProperty("round")]
        public long RoundId { get; set; }
        [JsonProperty("serviceName")]
        public string ServiceName { get; set; }
        public long DurationInMillis { get; set; }
        public static EnoStatisticMessage Create(long roundId, string serviceName, long duration)
        {
            return new EnoStatisticMessage()
            {
                CalculateServiceStatsFetchFinishedMessage = new CalculateServiceStatsFetchFinishedMessage()
                {
                    DurationInMillis = duration,
                    RoundId = roundId,
                    ServiceName = serviceName
                }
            };
        }
    }
}
