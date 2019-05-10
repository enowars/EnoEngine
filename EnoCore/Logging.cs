using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            Console.WriteLine(message.Message);
        }
    }
}
