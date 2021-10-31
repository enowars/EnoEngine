#pragma warning disable SA1200 // Using directives should be placed correctly
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnoCore;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Models.JsonConfiguration;
using EnoDatabase;
using EnoFlagSink;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#pragma warning restore SA1200 // Using directives should be placed correctly

const string mutexId = @"Global\EnoFlagSink";

CancellationTokenSource cancelSource = new CancellationTokenSource();

using var mutex = new Mutex(false, mutexId, out bool _);

try
{
    // Check if another EnoFlagSink is already running
    if (!mutex.WaitOne(10, false))
    {
        Console.WriteLine("Another Instance is already running.");
        return 1;
    }

    // Set up dependency injection tree
    var serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddSingleton(typeof(EnoDbUtil))
        .AddSingleton<FlagSubmissionEndpoint>()
        .AddSingleton(new EnoStatistics("EnoFlagSink"))
        .AddScoped<EnoDatabase.EnoDb>()
        .AddDbContextPool<EnoDbContext>(
            options =>
            {
                options.UseNpgsql(EnoDbContext.PostgresConnectionString);
            },
            10)
        .AddLogging(loggingBuilder =>
        {
            loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
            loggingBuilder.AddConsole();
            loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoFlagSink", cancelSource.Token));
        })
        .BuildServiceProvider(validateScopes: true);

    var submissionEndpoint = serviceProvider.GetRequiredService<FlagSubmissionEndpoint>();
    await submissionEndpoint.Start(cancelSource.Token);
}
finally
{
    mutex?.Close();
}

return 0;
