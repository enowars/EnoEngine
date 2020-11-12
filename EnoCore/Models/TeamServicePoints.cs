namespace EnoCore.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// The points of a particular team in a particular service.
    /// PK: TeamId, ServiceId
    /// </summary>
    public sealed record TeamServicePoints(long TeamId,
        long ServiceId,
        double AttackPoints,
        double DefensePoints,
        double ServiceLevelAgreementPoints,
        ServiceStatus Status,
        string? ErrorMessage);
}
