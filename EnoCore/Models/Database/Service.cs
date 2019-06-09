using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace EnoCore.Models
{
    public class Service
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }
        public string Name { get; set; }
        public int FlagsPerRound { get; set; }
        public int NoisesPerRound { get; set; }
        public int HavocsPerRound { get; set; }
        public long ServiceStatsId { get; set; }
        public List<ServiceStats> ServiceStats { get; set; }
        public bool Active { get; set; }
    }
}
