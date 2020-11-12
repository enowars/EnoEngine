namespace EnoCore.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public interface IEnoLogMessageProvider
    {
        IExternalScopeProvider? ScopeProvider { get; }
        string Tool { get; }
        void Log(string data);
    }
}
