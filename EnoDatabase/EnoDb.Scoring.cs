using System.Text.RegularExpressions;
using EnoCore.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace EnoDatabase; // #pragma warning disable SA1118

public record TeamResults(long TeamId, long ServiceId, long RoundId, double AttackPoints, double LostDefensePoints, double ServiceLevelAgreementPoints);
public record Results(long TeamId, long ServiceId, double Points);
public record SLAResults(
    long TeamId,
    long ServiceId,
    double Points,
    TeamServicePointsSnapshot? Snapshot,
    ServiceStatus Status);
