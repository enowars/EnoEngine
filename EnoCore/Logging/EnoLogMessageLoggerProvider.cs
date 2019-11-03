using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EnoCore.Logging
{
    public class EnoLogMessageLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        public IExternalScopeProvider? ScopeProvider { get; internal set; }
        public string Tool { get; }
        private readonly FileQueue Queue;

        public EnoLogMessageLoggerProvider(string tool, CancellationToken token)
        {
            Tool = tool;
            Queue = new FileQueue($"../data/{tool}.log", token);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new EnoLogMessageLogger(this, categoryName);
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            ScopeProvider = scopeProvider;
        }

        public void Dispose() { }

        public void Log(string data)
        {
            Queue.Enqueue(data);
        }
    }
}
