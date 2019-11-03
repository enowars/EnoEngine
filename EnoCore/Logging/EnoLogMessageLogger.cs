using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace EnoCore.Logging
{
    public class EnoLogMessageLogger : ILogger
    {
        public EnoLogMessageLoggerProvider Provider { get; }
        public string CategoryName { get;  }

        public EnoLogMessageLogger(EnoLogMessageLoggerProvider provider, string categoryName)
        {
            Provider = provider;
            CategoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state)
        {
            return Provider.ScopeProvider?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if ((this as ILogger).IsEnabled(logLevel))
            {
                EnoLogMessage message = new EnoLogMessage
                {
                    Message = exception?.Message ?? state?.ToString() ?? "",
                    Module = CategoryName,
                    Tool = Provider.Tool,
                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                if (Provider.ScopeProvider != null)
                {
                    Provider.ScopeProvider.ForEachScope((value, loggingProps) =>
                    {
                        if (value is IEnumerable<KeyValuePair<string, object>> props)
                        {
                            foreach (var pair in props)
                            {
                                if (pair.Value is CheckerTask task)
                                    message.FromCheckerTask(task);
                            }
                        }
                    },
                    state);
                }
                Provider.Log($"##ENOLOGMESSAGE {JsonConvert.SerializeObject(message)}\n");
            }
        }
    }
}
