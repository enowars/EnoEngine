namespace SampleChecker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using EnoCore.Checker;
    using Microsoft.Extensions.DependencyInjection;

    public class SampleCheckerInitializer : ICheckerInitializer
    {
        public int FlagVariants => 1;

        public int NoiseVariants => 1;

        public int HavocVariants => 1;

        public string ServiceName => "Sample";

        public void Initialize(IServiceCollection collection)
        {
            collection.AddSingleton(typeof(SampleSingleton));
        }
    }
}
