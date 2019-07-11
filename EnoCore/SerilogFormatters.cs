using EnoCore.Models.Json;
using Newtonsoft.Json;
using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace EnoCore
{
    public class EnoCoreTextFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            var ctx = logEvent.Properties.GetValueOrDefault("SourceContext") as ScalarValue;
            output.Write($"[{ctx?.Value}] {logEvent.RenderMessage()}{output.NewLine}");
        }
    }

    public class EnoCoreJsonFormatter : ITextFormatter
    {
        private readonly string Tool;

        public EnoCoreJsonFormatter(string tool)
        {
            Tool = tool;
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            var enomessage = EnoLogMessage.FromLogEvent(logEvent);
            enomessage.Tool = Tool;
            enomessage.Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var ctx = logEvent.Properties.GetValueOrDefault("SourceContext") as ScalarValue;
            enomessage.Module = ctx?.Value.ToString();
            enomessage.Severity = logEvent.Level.ToString();
            output.Write($"{JsonConvert.SerializeObject(enomessage)}{output.NewLine}");
        }
    }
}
