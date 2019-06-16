using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace EnoCore
{
    public class EnoLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new EnoConsoleLogger(categoryName);
        }

        public void Dispose() { }
    }

    public class EnoConsoleLogger : ILogger
    {
        public EnoConsoleLogger(string categoryName) { }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel >= LogLevel.Information)
            {
                Console.WriteLine(formatter(state, exception));
            }
        }
    }

    public class EnoLogger
    {
        private readonly string Tool;

        public EnoLogger(string tool)
        {
            Tool = tool;
        }

        public void LogTrace(EnoLogMessage message)
        {
            message.Severity = "TRACE";
            Log(message);
        }

        public void LogDebug(EnoLogMessage message)
        {
            message.Severity = "DEBUG";
            Log(message);
        }

        public void LogInfo(EnoLogMessage message)
        {
            message.Severity = "INFO";
            Log(message);
        }

        public void LogWarning(EnoLogMessage message)
        {
            message.Severity = "WARNING";
            Log(message);
        }

        public void LogError(EnoLogMessage message)
        {
            message.Severity = "ERROR";
            Log(message);
        }

        public void LogFatal(EnoLogMessage message)
        {
            message.Severity = "FATAL";
            Log(message);
        }

        private void Log(EnoLogMessage message)
        {
            message.Tool = Tool;
            message.Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            Debug.WriteLine(JsonConvert.SerializeObject(message));
            Console.WriteLine($"{message.Timestamp} {message.Message}");
        }
    }
}
