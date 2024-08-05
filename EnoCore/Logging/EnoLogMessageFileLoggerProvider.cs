namespace EnoCore.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    public sealed class EnoLogMessageFileLoggerProvider : ILoggerProvider, ISupportExternalScope, IEnoLogMessageProvider, IDisposable
    {
        private readonly FileQueue queue;
        private readonly string tool;

        public EnoLogMessageFileLoggerProvider(string tool, CancellationToken token)
        {
            this.tool = tool;
            this.queue = new FileQueue($"../data/{tool}.log.gz", token);
        }

        public IExternalScopeProvider? ScopeProvider { get; internal set; }

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
            this.queue.Dispose();
        }

        public void Log(string data)
        {
            this.queue.Enqueue(data);
        }
    }
}
