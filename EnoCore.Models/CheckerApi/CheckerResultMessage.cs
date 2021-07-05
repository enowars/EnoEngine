using EnoCore.Models.Database;

namespace EnoCore.Models.CheckerApi
{
    public sealed record CheckerResultMessage(
        CheckerResult Result,
        string? Message,
        string? AttackInfo);
}
