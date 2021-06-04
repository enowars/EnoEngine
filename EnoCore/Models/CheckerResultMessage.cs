namespace EnoCore.Models
{
    public sealed record CheckerResultMessage(
        CheckerResult Result,
        string? Message,
        string? AttackInfo);
}
