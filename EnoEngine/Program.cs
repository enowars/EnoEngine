const string MODE_RECALCULATE = "recalculate";

const string mutexId = @"Global\EnoEngine";

CancellationTokenSource cancelSource = new();

using var mutex = new Mutex(false, mutexId, out bool _);

try
{
    // Check if another EnoEngine is already running
    if (!mutex.WaitOne(10, false))
    {
        Console.Error.WriteLine("Another Instance is already running.");
        return 1;
    }

    // Set up dependency injection tree
    var serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddSingleton(typeof(EnoDbUtil))
        .AddSingleton(new EnoStatistics(nameof(EnoEngine)))
        .AddScoped<EnoDatabase.EnoDb>()
        .AddSingleton<EnoEngine.EnoEngine>()
        .AddDbContextPool<EnoDbContext>(
            options =>
            {
                options.UseNpgsql(EnoDbContext.PostgresConnectionString);
            },
            90)
        .AddLogging(loggingBuilder =>
        {
            loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
            loggingBuilder.AddConsole();
            loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoEngine", cancelSource.Token));
        })
        .BuildServiceProvider(validateScopes: true);

    // Go!
    var engine = serviceProvider.GetRequiredService<EnoEngine.EnoEngine>();
    engine.RunContest().Wait();
}
finally
{
    mutex?.Close();
}

return 0;
