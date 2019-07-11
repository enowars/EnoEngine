using EnoCore;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EnoEngine
{
    class Program
    {
        public static void Main(string argument = null)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Override(DbLoggerCategory.Name, Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Async(a => a.File(new EnoCoreJsonFormatter("EnoEngine"), "../data/engine.log", fileSizeLimitBytes: null))
                .WriteTo.Console(new EnoCoreTextFormatter())
                .CreateLogger();
            var serviceProvider = new ServiceCollection()
                .AddEnoEngine()
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddFilter((category, level) => category != DbLoggerCategory.Database.Command.Name);
                    loggingBuilder.AddSerilog(dispose: true);
                })
                .BuildServiceProvider(validateScopes: true);

            var content = File.ReadAllText("ctf.json");
            EnoEngine.Configuration = JsonConvert.DeserializeObject<JsonConfiguration>(content);

            var engine = serviceProvider.GetRequiredService<IEnoEngine>();
            if (argument == EnoEngine.MODE_RECALCULATE)
            {
                engine.RunRecalculation().Wait();
            }
            else
            {
                engine.RunContest().Wait();
            }
        }
    }
}
