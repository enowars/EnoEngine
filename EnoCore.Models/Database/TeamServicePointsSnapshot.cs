namespace EnoCore.Models.Database;

/// <summary>
/// The fixed points of a particular team in a particular service.
/// </summary>
public sealed record TeamServicePointsSnapshot(long TeamId,
    long ServiceId,
    double AttackPoints,
    double LostDefensePoints,
    double ServiceLevelAgreementPoints,
    long RoundId);
