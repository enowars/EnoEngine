using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models
{
    public class EnoEngineScoreboardService
    {
#pragma warning disable CS8618
        public long ServiceId { get; set; }
        public string ServiceName { get; set; }
        public long MaxStores { get; set; }
        public EnoScoreboardFirstblood[] Firstbloods { get; private set; }
        public EnoEngineScoreboardService(EnoScoreboardFirstblood[] firstbloods, Service service)
        {
            Firstbloods = firstbloods;
            MaxStores = service.FlagsPerRound;
            ServiceId = service.Id;
            ServiceName = service.Name;
        }
#pragma warning restore CS8618
    }
}
