using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EnoCore.Logging
{
    public interface IEnoLogMessageProvider
    {
        void Log(string data);
        IExternalScopeProvider? ScopeProvider { get; }
        string Tool { get; }
    }
}
