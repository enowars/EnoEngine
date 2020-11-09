using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    public class Service
    {
        public long Id { get; set;}
        public string Name { get; set;}
        public long FlagsPerRound { get; set;}
        public long NoisesPerRound { get; set;}
        public long HavocsPerRound { get; set;}
        public long FlagStores { get; set;}
        public bool Active { get; set; }

        public Service(long id,
            string name,
            long flagsPerRound,
            long noisesPerRound,
            long havocsPerRound,
            long flagStores,
            bool active)
        {
            Id = id;
            Name = name;
            FlagsPerRound = flagsPerRound;
            NoisesPerRound = noisesPerRound;
            HavocsPerRound = havocsPerRound;
            FlagStores = flagStores;
            Active = active;
        }
    }
}
