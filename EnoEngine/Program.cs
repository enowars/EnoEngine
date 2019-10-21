using EnoCore;
using EnoCore.Logging;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace EnoEngine
{
    class Program
    {
        private static CancellationTokenSource CancelSource = new CancellationTokenSource();

        public static void Main(string argument = null)
        {
            var serviceProvider = new ServiceCollection()
                .AddEnoEngine()
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
                    loggingBuilder.AddConsole();
                    loggingBuilder.AddProvider(new EnoLogMessageLoggerProvider("EnoEngine", CancelSource.Token));
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
