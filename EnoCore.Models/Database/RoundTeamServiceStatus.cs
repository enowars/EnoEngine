namespace EnoCore.Models.Database;

public enum ServiceStatus
{
    INTERNAL_ERROR,
    OK,
    RECOVERING,
    MUMBLE,
    OFFLINE,
    INACTIVE,
}

/// <summary>
/// The ServiceStatus for one particular service, team, and round.
/// </summary>
#pragma warning disable SA1201 // Elements should appear in the correct order
public sealed record RoundTeamServiceStatus(ServiceStatus Status,
#pragma warning restore SA1201 // Elements should appear in the correct order
    string? ErrorMessage,
    long TeamId,
    long ServiceId,
    long GameRoundId);
