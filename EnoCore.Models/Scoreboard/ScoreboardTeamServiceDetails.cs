namespace EnoCore.Models.Scoreboard;

public class ScoreboardTeamServiceDetails
{
    public ScoreboardTeamServiceDetails(long serviceId, double attackScore, double defenseScore, double serviceLevelAgreementScore, ServiceStatus serviceStatus, string? message)
    {
        this.ServiceId = serviceId;
        this.AttackScore = attackScore;
        this.DefenseScore = defenseScore;
        this.ServiceLevelAgreementScore = serviceLevelAgreementScore;
        this.ServiceStatus = serviceStatus;
        this.Message = message;
    }

    /// <summary>
    ///  The id of the service.
    /// </summary>
    [Required]
    public long ServiceId { get; init; }

    /// <summary>
    /// The attack Score of the team for this service.
    /// </summary>
    [Required]
    public double AttackScore { get; init; }

    /// <summary>
    ///  The defense Score of the team for this service.
    /// </summary>
    [Required]
    public double DefenseScore { get; init; }

    /// <summary>
    /// The SLA Score of the team for this service.
    /// </summary>
    [Required]
    public double ServiceLevelAgreementScore { get; init; }

    /// <summary>
    /// The Teams status for this service.
    /// </summary>
    /// <example>"INTERNAL_ERROR", "OFFLINE", "MUMBLE", "RECOVERING", "OK", "INACTIVE".</example>
    [Required]
    public ServiceStatus ServiceStatus { get; init; }

    /// <summary>
    /// The message of the service, otherwise null.
    /// </summary>
    public string? Message { get; init; }
}
