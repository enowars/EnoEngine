namespace EnoCore.Models
{
    public sealed record CheckerTaskMessage(
        long TaskId,
        CheckerTaskMethod Method,
        string Address,
        long TeamId,
        string TeamName,
        long CurrentRoundId,
        long RelatedRoundId,
        string? Flag,
        long VariantId,
        long Timeout,
        long RoundLength,
        string TaskChainId);
}
