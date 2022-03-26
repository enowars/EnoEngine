namespace EnoCore.Models.Database;

public sealed record Round(long Id,
    DateTimeOffset Begin,
    DateTimeOffset Quarter2,
    DateTimeOffset Quarter3,
    DateTimeOffset Quarter4,
    DateTimeOffset End);
