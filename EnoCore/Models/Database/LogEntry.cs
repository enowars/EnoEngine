using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Database
{
    public enum LogSeverity
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public class CheckerLogMessage
    {
        public long Id { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public LogSeverity Severity { get; set; }
        public CheckerTask RelatedTask { get; set; }
        public long RelatedTaskId { get; set; }
        public string Origin { get; set; }
    }
}
