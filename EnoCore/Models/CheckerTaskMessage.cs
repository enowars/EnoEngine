namespace EnoCore.Models
{
    public sealed record CheckerTaskMessage(
        long RunId,
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
        long RoundLength);
}
