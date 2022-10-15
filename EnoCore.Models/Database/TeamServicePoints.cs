namespace EnoCore.Models.Database;

/// <summary>
/// The points of a particular team in a particular service.
/// PK: TeamId, ServiceId.
/// </summary>
public sealed class TeamServicePoints
{
    public TeamServicePoints(
        long teamId,
        long serviceId,
        double attackPoints,
        double defensePoints,
        double serviceLevelAgreementPoints,
        ServiceStatus status,
        string? errorMessage)
    {
        this.TeamId = teamId;
        this.ServiceId = serviceId;
        this.AttackPoints = attackPoints;
        this.DefensePoints = defensePoints;
        this.ServiceLevelAgreementPoints = serviceLevelAgreementPoints;
        this.Status = status;
        this.ErrorMessage = errorMessage;
    }

    public long TeamId { get; set; }

    public long ServiceId { get; set; }

    public double AttackPoints { get; set; }

    public double DefensePoints { get; set; }

    public double ServiceLevelAgreementPoints { get; set; }

    public ServiceStatus Status { get; set; }

    public string? ErrorMessage { get; set; }
}
