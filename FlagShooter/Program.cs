﻿using EnoCore;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace FlagShooter
{
    class Program
    {

        public Program(ServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }
        }

        public void Start()
        {
            Client.Timeout = new TimeSpan(0, 1, 0);
            LauncherLoop().Wait();
        }

        public async Task LauncherLoop()
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }

            Logger.LogInfo(new EnoLogMessage()
            {
                Module = nameof(EnoLauncher),
                Function = nameof(LauncherLoop),
                Message = $"LauncherLoop starting"
            });

            while (!LauncherCancelSource.IsCancellationRequested)
            {
                try
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                        
                        var flags = await db.RetrieveFlags(1000, new List());
                        if (flags.Count > 0)
                        {
                            Logger.LogDebug(new EnoLogMessage()
                            {
                                Module = nameof(EnoLauncher),
                                Function = nameof(LauncherLoop),
                                Message = $"Sending {flags.Count} flags"
                            });
                        }
                        foreach (var flag in flags)
                        {
                            var t = Task.Run(async () => await SendFlagTask(flag));
                        }
                        if (flags.Count == 0)
                        {
                            await Task.Delay(50, LauncherCancelSource.Token);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning(new EnoLogMessage()
                    {
                        Module = nameof(EnoLauncher),
                        Function = nameof(LauncherLoop),
                        Message = $"LauncherLoop retrying because: {EnoCoreUtils.FormatException(e)}"
                    });
                }
            }
        }

        private Task SendFlagTask(Flag f)
        {
            var client = TcpClient("localhost", 1337);
            var stream = client.GetStream();
            var flagstr = f.StringRepresentation+"\n";
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(flagstr);    
            stream.Write(data, 0, data.Length);
            stream.Close();
            client.Close();
        }


        static void Main(string[] args)
        {
            try
            {
                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(EnoLauncher),
                    Function = nameof(Main),
                    Message = $"FlagShooter starting"
                });
                var serviceProvider = new ServiceCollection()
                    .AddDbContextPool<EnoDatabaseContext>(options => {
                        options.UseNpgsql(
                            EnoCoreUtils.PostgresConnectionString,
                            pgoptions => pgoptions.EnableRetryOnFailure());
                    }, 2)
                    .AddScoped<IEnoDatabase, EnoDatabase>()
                    .BuildServiceProvider(validateScopes: true);
                new Program(serviceProvider).Start();
            }
            catch (Exception e)
            {
                Logger.LogFatal(new EnoLogMessage()
                {
                    Module = nameof(EnoLauncher),
                    Function = nameof(Main),
                    Message = $"FlagShooter failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
        }
    }
}