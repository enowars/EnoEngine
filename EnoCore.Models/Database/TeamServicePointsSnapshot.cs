namespace EnoCore.Models.Database;

/// <summary>
/// The fixed points of a particular team in a particular service in a particular round.
/// </summary>
public sealed record TeamServicePointsSnapshot
{
    public TeamServicePointsSnapshot(
        long teamId,
        long serviceId,
        double attackPoints,
        double lostDefensePoints,
        double serviceLevelAgreementPoints,
        long roundId)
    {
        this.TeamId = teamId;
        this.ServiceId = serviceId;
        this.AttackPoints = attackPoints;
        this.LostDefensePoints = lostDefensePoints;
        this.ServiceLevelAgreementPoints = serviceLevelAgreementPoints;
        this.RoundId = roundId;
    }

    public long TeamId { get; set; }

    public long ServiceId { get; set; }

    public double AttackPoints { get; set; }

    public double LostDefensePoints { get; set; }

    public double ServiceLevelAgreementPoints { get; set; }

    public long RoundId { get; set; }
}
