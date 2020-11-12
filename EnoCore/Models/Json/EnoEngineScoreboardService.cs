namespace EnoCore.Models.Json
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using EnoCore.Models.Database;

    public record EnoEngineScoreboardService(
        long ServiceId,
        string ServiceName,
        long MaxStores,
        EnoScoreboardFirstblood[] FirstBloods);
}
