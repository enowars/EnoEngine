namespace EnoCore.Models.Database
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// The fixed points of a particular team in a particular service
    /// </summary>
    public sealed record TeamServicePointsSnapshot(long TeamId,
        long ServiceId,
        double AttackPoints,
        double LostDefensePoints,
        double ServiceLevelAgreementPoints,
        long RoundId);
}
