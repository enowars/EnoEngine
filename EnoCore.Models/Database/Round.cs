namespace EnoCore.Models.Database;

public sealed record Round(long Id,
    DateTime Begin,
    DateTime Quarter2,
    DateTime Quarter3,
    DateTime Quarter4,
    DateTime End);
