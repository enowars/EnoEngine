namespace EnoDatabase;

public class EnoDbUtil
{
    private readonly ILogger<EnoDbUtil> logger;
    private readonly IServiceProvider serviceProvider;

    public EnoDbUtil(IServiceProvider serviceProvider, ILogger<EnoDbUtil> logger)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    public async Task ExecuteScopedDatabaseActionIgnoreErrors(Func<EnoDb, Task> function)
    {
        try
        {
            using var scope = this.serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
            await function(db);
        }
        catch (Exception e)
        {
            this.logger.LogError($"ExecuteScopedDatabaseActionIgnoreErrors ignoring Exception:\n{e}");
        }
    }

    public async Task<T> ExecuteScopedDatabaseAction<T>(Func<EnoDb, Task<T>> function)
    {
        using var scope = this.serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
        return await function(db);
    }

    public async Task RetryScopedDatabaseAction(Func<EnoDb, Task> function)
    {
        Exception? lastException = null;
        for (int i = 0; i < EnoDbContext.DatabaseRetries; i++)
        {
            try
            {
                using var scope = this.serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
                await function(db);
                return;
            }
            catch (SocketException e)
            {
                this.logger.LogError($"{nameof(this.RetryScopedDatabaseAction)} caught an exception: {e.ToFancyString()}");
                lastException = e;
            }
            catch (IOException e)
            {
                this.logger.LogError($"{nameof(this.RetryScopedDatabaseAction)} caught an exception: {e.ToFancyString()}");
                lastException = e;
            }
        }

        throw new Exception($"{nameof(this.RetryScopedDatabaseAction)} giving up after {EnoDbContext.DatabaseRetries} retries", lastException);
    }

    public async Task<T> RetryScopedDatabaseAction<T>(Func<EnoDb, Task<T>> function)
    {
        Exception? lastException = null;
        for (int i = 0; i < EnoDbContext.DatabaseRetries; i++)
        {
            try
            {
                using var scope = this.serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EnoDb>();
                return await function(db);
            }
            catch (SocketException e)
            {
                this.logger.LogError($"{nameof(this.RetryScopedDatabaseAction)} caught an exception: {e.ToFancyString()}");
                lastException = e;
            }
            catch (IOException e)
            {
                this.logger.LogError($"{nameof(this.RetryScopedDatabaseAction)} caught an exception: {e.ToFancyString()}");
                lastException = e;
            }
        }

        throw new Exception($"{nameof(this.RetryScopedDatabaseAction)} giving up after {EnoDbContext.DatabaseRetries} retries", lastException);
    }
}
