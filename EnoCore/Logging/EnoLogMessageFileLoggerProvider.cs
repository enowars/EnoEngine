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

        public EnoLogMessageFileLoggerProvider(string tool, CancellationToken token)
        {
            this.Tool = tool;
            this.queue = new FileQueue($"../data/{tool}.log", token);
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
            this.queue.Dispose();
        }

        public void Log(string data)
        {
            this.queue.Enqueue(data);
        }
    }
}
