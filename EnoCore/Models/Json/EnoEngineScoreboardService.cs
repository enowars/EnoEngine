using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class EnoEngineScoreboardService
    {
#pragma warning disable CS8618
        public long ServiceId { get; set; }
        public string ServiceName { get; set; }
        public long MaxStores { get; set; }
        public EnoScoreboardFirstblood[] FirstBloods { get; private set; }
        public EnoEngineScoreboardService(EnoScoreboardFirstblood[] firstBloods, Service service)
        {
            FirstBloods = firstBloods;
            MaxStores = service.FetchedFlagsPerRound;
            ServiceId = service.Id;
            ServiceName = service.Name;
        }
#pragma warning restore CS8618
    }
}
