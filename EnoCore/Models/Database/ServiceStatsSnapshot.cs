using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    public sealed record ServiceStatsSnapshot(long TeamId,
        long ServiceId,
        double AttackPoints,
        double LostDefensePoints,
        double ServiceLevelAgreementPoints,
        long RoundId);
}
