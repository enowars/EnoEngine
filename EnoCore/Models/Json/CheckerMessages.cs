using EnoCore.Models.Database;
using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public sealed record CheckerResultMessage(CheckerResult Result,
        string? Message);

    public sealed record CheckerInfoMessage(string ServiceName,
        int FlagCount,
        int NoiseCount,
        int HavocCount);

    public sealed record CheckerTaskMessage(long RunId,
        CheckerTaskMethod Method,
        string Address,
        long ServiceId,
        string ServiceName,
        long TeamId,
        string TeamName,
        long RelatedRoundId,
        long RoundId,
        string? Flag,
        long FlagIndex,
        long Timeout,
        long RoundLength)
    {
        public static CheckerTaskMessage FromCheckerTask(CheckerTask task)
        {
            return new(task.Id,
                task.Method,
                task.Address,
                task.ServiceId,
                task.ServiceName,
                task.TeamId,
                task.TeamName,
                task.RelatedRoundId,
                task.CurrentRoundId,
                task.Payload,
                task.TaskIndex,
                task.MaxRunningTime,
                task.RoundLength);
        }
    }
}
