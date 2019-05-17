using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models
{
    public class Service
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int FlagsPerRound { get; set; }
        public int NoisesPerRound { get; set; }
        public long ServiceStatsId { get; set; }
        public List<ServiceStats> ServiceStats { get; set; }
    }
}
