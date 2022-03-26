namespace EnoCore.Models.Database;

using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// PK: FlagServiceId, sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset, sf.AttackerTeamId.
/// </summary>
public sealed record SubmittedFlag(
    long FlagServiceId,
    long FlagOwnerId,
    long FlagRoundId,
    int FlagRoundOffset,
    long AttackerTeamId,
    long RoundId,
    long SubmissionsCount,
    DateTimeOffset Timestamp);
