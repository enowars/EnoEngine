namespace EnoCore.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    public sealed class EnoLogMessageConsoleLoggerProvider : ILoggerProvider, ISupportExternalScope, IEnoLogMessageProvider
    {
        private readonly string tool;

        public EnoLogMessageConsoleLoggerProvider(string tool)
        {
            this.tool = tool;
        }

        public IExternalScopeProvider? ScopeProvider { get; set; }

        public ILogger CreateLogger(string categoryName)
        {
            return new EnoLogger(this, categoryName, this.tool);
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
