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
        public string ServiceName { get; }

        public void Initialize(IServiceCollection collection);
    }
}
