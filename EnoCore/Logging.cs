using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace EnoCore
{
    class EnoEngineConsoleLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new EnoEngineConsoleLogger(categoryName);
        }

        public void Dispose()
        {

        }
    }

    public class EnoEngineConsoleLogger : ILogger
    {
        private readonly string ClassName;
        public EnoEngineConsoleLogger(string categoryName)
        {
            ClassName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var line = string.Format("{0:s} [{1}] [{2}] ", DateTime.UtcNow, logLevel, ClassName) + formatter(state, exception);
            Debug.WriteLine(line);
            Console.WriteLine(line);
        }
    }
}
