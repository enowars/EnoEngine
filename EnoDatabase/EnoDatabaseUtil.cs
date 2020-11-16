namespace EnoDatabase
{
    using System;
    using System.Buffers;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class EnoDatabaseUtil
    {
        private readonly ILogger<EnoDatabaseUtil> logger;

        public EnoDatabaseUtil(ILogger<EnoDatabaseUtil> logger)
        {
            this.logger = logger;
        }

        public async Task RetryScopedDatabaseAction(IServiceProvider serviceProvider, Func<IEnoDatabase, Task> function)
        {
            Exception? lastException = null;
            for (int i = 0; i < EnoDatabaseContext.DatabaseRetries; i++)
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
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

            throw new Exception($"{nameof(this.RetryScopedDatabaseAction)} giving up after {EnoDatabaseContext.DatabaseRetries} retries", lastException);
        }

        public async Task<T> RetryScopedDatabaseAction<T>(IServiceProvider serviceProvider, Func<IEnoDatabase, Task<T>> function)
        {
            Exception? lastException = null;
            for (int i = 0; i < EnoDatabaseContext.DatabaseRetries; i++)
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
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

            throw new Exception($"{nameof(this.RetryScopedDatabaseAction)} giving up after {EnoDatabaseContext.DatabaseRetries} retries", lastException);
        }
    }
}
