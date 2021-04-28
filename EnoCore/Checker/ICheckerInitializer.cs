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
        int FlagVariants { get; }

        int NoiseVariants { get; }

        int HavocVariants { get; }

        public string ServiceName { get; }

        public void Initialize(IServiceCollection collection);
    }
}
