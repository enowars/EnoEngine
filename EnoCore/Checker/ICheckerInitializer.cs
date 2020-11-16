namespace EnoCore.Checker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    public interface ICheckerInitializer
    {
        int FlagsPerRound { get; }

        int NoisesPerRound { get; }

        int HavocsPerRound { get; }

        public string ServiceName { get; }

        public void Initialize(IServiceCollection collection);
    }
}
