namespace EnoCore.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using EnoCore.Models.Database;
    using EnoCore.Models.Json;
    using Microsoft.Extensions.Logging;

    public class EnoLogger : ILogger
    {
        private readonly JsonSerializerOptions jsonOptions;

        public EnoLogger(IEnoLogMessageProvider provider, string categoryName)
        {
            this.Provider = provider;
            this.CategoryName = categoryName;
            this.jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
        }

        public IEnoLogMessageProvider Provider { get; init; }
        public string CategoryName { get; init; }

        public static string GetSeverity(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.None => "DEBUG",
                LogLevel.Trace => "DEBUG",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARNING",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRITICAL",
                _ => throw new InvalidOperationException(),
            };
        }

        public static long GetSeverityLevel(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.None => 0,
                LogLevel.Trace => 0,
                LogLevel.Debug => 0,
                LogLevel.Information => 1,
                LogLevel.Warning => 2,
                LogLevel.Error => 3,
                LogLevel.Critical => 4,
                _ => throw new InvalidOperationException(),
            };
        }

        public IDisposable? BeginScope<TState>(TState state)
        {
            return this.Provider.ScopeProvider?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (this.IsEnabled(logLevel))
            {
                string message = exception?.Message ?? state?.ToString() ?? string.Empty;
                string module = this.CategoryName;
                string tool = this.Provider.Tool;
                string timestamp = EnoCoreUtil.GetCurrentTimestamp();
                string severity = GetSeverity(logLevel);
                long severityLevel = GetSeverityLevel(logLevel);

                if (this.Provider.ScopeProvider != null)
                {
                    this.Provider.ScopeProvider.ForEachScope(
                        (value, loggingProps) =>
                        {
                            if (value is IEnumerable<KeyValuePair<string, object>> props)
                            {
                                foreach (var pair in props)
                                {
                                    if (pair.Value is CheckerTask task)
                                    {
                                        var enoLogMessage = EnoLogMessage.FromCheckerTask(task, tool, severity, severityLevel, module, null, message);
                                        this.Provider.Log($"##ENOLOGMESSAGE {JsonSerializer.Serialize(enoLogMessage, this.jsonOptions)}\n");
                                        return;
                                    }
                                    else if (pair.Value is CheckerTaskMessage taskMessage)
                                    {
                                        var enoLogMessage = EnoLogMessage.FromCheckerTaskMessage(taskMessage, tool, severity, severityLevel, module, null, message);
                                        this.Provider.Log($"##ENOLOGMESSAGE {JsonSerializer.Serialize(enoLogMessage, this.jsonOptions)}\n");
                                        return;
                                    }
                                }
                            }
                        },
                        state);
                }
            }
        }
    }
}
