using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EnoCore.Logging
{
    public class EnoLogMessageConsoleLoggerProvider : ILoggerProvider, ISupportExternalScope, IEnoLogMessageProvider
    {
        public IExternalScopeProvider? ScopeProvider { get; internal set; }
        public string Tool { get; }

        public EnoLogMessageConsoleLoggerProvider(string tool, CancellationToken _)
        {
            Tool = tool;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new EnoLogMessageConsoleLogger(this, categoryName);
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            ScopeProvider = scopeProvider;
        }

        public void Dispose() { }

        public void Log(string data)
        {
            Console.Write(data);
        }
    }
}
