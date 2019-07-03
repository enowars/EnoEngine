using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace EnoCore
{
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
            LogEno(message);
        }

        public void LogDebug(EnoLogMessage message)
        {
            message.Severity = "DEBUG";
            LogEno(message);
        }

        public void LogInfo(EnoLogMessage message)
        {
            message.Severity = "INFO";
            LogEno(message);
        }

        public void LogWarning(EnoLogMessage message)
        {
            message.Severity = "WARNING";
            LogEno(message);
        }

        public void LogError(EnoLogMessage message)
        {
            message.Severity = "ERROR";
            LogEno(message);
        }

        public void LogFatal(EnoLogMessage message)
        {
            message.Severity = "FATAL";
            LogEno(message);
        }

        private void LogEno(EnoLogMessage message)
        {
            message.Tool = Tool;
            message.Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            Log.Logger.Information(JsonConvert.SerializeObject(message));
            Console.WriteLine($"{message.Timestamp} {message.Message}");
        }

        public void LogStatistics(EnoStatisticMessage message)
        {
            message.Tool = Tool;
            Log.Logger.Information(JsonConvert.SerializeObject(message));
        }
    }
}
