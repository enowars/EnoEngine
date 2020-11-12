namespace EnoCore.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    public sealed class EnoLogMessageConsoleLoggerProvider : ILoggerProvider, ISupportExternalScope, IEnoLogMessageProvider
    {
        public EnoLogMessageConsoleLoggerProvider(string tool)
        {
            this.Tool = tool;
        }

        public IExternalScopeProvider? ScopeProvider { get; internal set; }
        public string Tool { get; }

        public ILogger CreateLogger(string categoryName)
        {
            return new EnoLogger(this, categoryName);
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            this.ScopeProvider = scopeProvider;
        }

        public void Dispose()
        {
        }

        public void Log(string data)
        {
            Console.Write(data);
        }
    }
}
