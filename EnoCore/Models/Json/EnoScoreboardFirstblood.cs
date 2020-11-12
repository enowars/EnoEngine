namespace EnoCore.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using EnoCore.Models.Database;

    public record EnoScoreboardFirstblood(
        long TeamId,
        string Timestamp,
        double TimeEpoch,
        long RoundId,
        string? StoreDescription,
        long StoreIndex);
}
