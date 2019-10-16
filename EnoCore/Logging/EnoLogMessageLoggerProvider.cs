using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Logging
{
    public class EnoLogMessageLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        public IExternalScopeProvider ScopeProvider { get; internal set; }
        public string Tool { get; }

        public EnoLogMessageLoggerProvider(string tool)
        {
            Tool = tool;
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
    }
}
