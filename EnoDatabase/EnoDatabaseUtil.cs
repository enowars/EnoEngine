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
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EnoDatabase
{
    public class EnoDatabaseUtil
    {
        public static async Task RetryScopedDatabaseAction(IServiceProvider serviceProvider, Func<IEnoDatabase, Task> function)
        {
            Exception? lastException = null;
            for (int i = 0; i < EnoDatabaseContext.DATABASE_RETRIES; i++)
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
                    lastException = e;
                }
                catch (IOException e)
                {
                    lastException = e;
                }
            }
#pragma warning disable CS8597
            throw lastException;
#pragma warning restore CS8597
        }

        public static async Task<T> RetryScopedDatabaseAction<T>(IServiceProvider serviceProvider, Func<IEnoDatabase, Task<T>> function)
        {
            Exception? lastException = null;
            for (int i = 0; i < EnoDatabaseContext.DATABASE_RETRIES; i++)
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                    return await function(db);
                }
                catch (SocketException e)
                {
                    lastException = e;
                }
                catch (IOException e)
                {
                    lastException = e;
                }
            }
#pragma warning disable CS8597
            throw lastException;
#pragma warning restore CS8597
        }
    }
}
