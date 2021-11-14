namespace EnoCore.Models.CheckerApi;

using EnoCore.Models.Database;

public sealed record CheckerResultMessage(
    CheckerResult Result,
    string? Message,
    string? AttackInfo);
