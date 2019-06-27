using EnoCore;
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
using EnoCore.Models;


namespace FlagShooter
{
    class Program
    {

        private static readonly CancellationTokenSource LauncherCancelSource = new CancellationTokenSource();
        private static readonly EnoLogger Logger = new EnoLogger(nameof(FlagShooter));
        private readonly ServiceProvider ServiceProvider;
        private readonly Dictionary<long, (TcpClient, StreamReader reader, StreamWriter writer)> TeamSockets =
            new Dictionary<long, (TcpClient, StreamReader reader, StreamWriter writer)>();
        private readonly long AttackingTeams = 10;

        public Program(ServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }
            for (int i = 0; i < AttackingTeams; i++)
            {
                var tcpClient = new TcpClient();
                tcpClient.Connect("localhost", 1338);
                (TcpClient tcpClient, StreamReader, StreamWriter writer) client = (tcpClient, new StreamReader(tcpClient.GetStream()), new StreamWriter(tcpClient.GetStream()));
                client.writer.AutoFlush = true;
                client.writer.Write($"{i+1}\n");
                TeamSockets[i+1] = client;
            }
        }

        public void Start()
        {
            FlagRunnerLoop().Wait();
        }

        public async Task FlagRunnerLoop()
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }

            Logger.LogInfo(new EnoLogMessage()
            {
                Module = nameof(FlagShooter),
                Function = nameof(FlagRunnerLoop),
                Message = $"FlagRunnerLoop starting"
            });

            var flagcount = 10000;
            while (!LauncherCancelSource.IsCancellationRequested)
            {
                try
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                        var flags = await db.RetrieveFlags(flagcount);
                        var tasks = new Task[AttackingTeams];
                        if (flags.Length > 0)
                        {
                            Logger.LogDebug(new EnoLogMessage()
                            {
                                Module = nameof(FlagShooter),
                                Function = nameof(FlagRunnerLoop),
                                Message = $"Sending {flags.Length} flags"
                            });
                        }
                        for (int i = 0; i < AttackingTeams; i++)
                        {
                            var ti = i;
                            tasks[ti] = Task.Run(async () => await SendFlagTask(flags, ti + 1));
                        }
                        await Task.WhenAll(tasks);
                        await Task.Delay(1000, LauncherCancelSource.Token);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning(new EnoLogMessage()
                    {
                        Module = nameof(FlagShooter),
                        Function = nameof(FlagRunnerLoop),
                        Message = $"FlagRunnerLoop retrying because: {EnoCoreUtils.FormatException(e)}"
                    });
                }
            }
        }

        private async Task SendFlagTask(Flag[] flags, long teamId)
        {
            var batchSize = 100;
            try
            {
                var (_, reader, writer) = TeamSockets[teamId];
                for (int i = 0; i < flags.Length / batchSize; i++)
                {
                    var submissionStringBuilder = new StringBuilder();
                    int j;
                    for (j = 0; j < batchSize; j++)
                    {
                        if (i + j < flags.Length)
                        {
                            submissionStringBuilder.Append($"{flags[i + j]}\n");
                        }
                    }
                    await writer.WriteAsync(submissionStringBuilder.ToString());
                    for (int k = 0; k < j; k++)
                    {
                        //Console.WriteLine($"Team {teamId} waiting...");
                        var resp = await reader.ReadLineAsync();
                        //Console.WriteLine($"{resp}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning(new EnoLogMessage()
                {
                    Module = nameof(FlagShooter),
                    Function = nameof(SendFlagTask),
                    Message = $"SendFlagTask failed because: {EnoCoreUtils.FormatException(e)}"
                });
            }
        }


        static void Main(string[] args)
        {
            try
            {
                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(FlagShooter),
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
                    Module = nameof(FlagShooter),
                    Function = nameof(Main),
                    Message = $"FlagShooter failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
        }
    }
}
