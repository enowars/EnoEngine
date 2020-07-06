using EnoCore;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnoCore.Models;
using System.Linq;
using EnoDatabase;

namespace FlagShooter
{
    class Program
    {
        private static readonly CancellationTokenSource LauncherCancelSource = new CancellationTokenSource();
        private readonly ServiceProvider ServiceProvider;
        private readonly Dictionary<long, (TcpClient, StreamReader reader, StreamWriter writer)[]> TeamSockets =
            new Dictionary<long, (TcpClient, StreamReader reader, StreamWriter writer)[]>();
        private readonly int FlagCount;
        private readonly int TeamStart;
        private readonly int RoundDelay;
        private readonly int TeamCount;
        private readonly int SubmissionConnectionsPerTeam;
        private readonly JsonConfiguration Configuration;

        public Program(ServiceProvider serviceProvider, int flagCount, int roundDelay, int teamStart, int teamCount, int teamConnections, JsonConfiguration configuration)
        {
            ServiceProvider = serviceProvider;
            FlagCount = flagCount;
            TeamStart = teamStart;
            RoundDelay = roundDelay;
            TeamCount = teamCount;
            SubmissionConnectionsPerTeam = teamConnections;
            Configuration = configuration;
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }
            Console.WriteLine("Initializing connections");
            for (int i = 0; i < TeamCount; i++)
            {
                Console.WriteLine($"Initializing team {i}");
                TeamSockets[i + 1] = new (TcpClient, StreamReader reader, StreamWriter writer)[SubmissionConnectionsPerTeam];
                for (int j = 0; j < SubmissionConnectionsPerTeam; j++)
                {
                    Console.WriteLine($"Initializing conn {j}");
                    var tcpClient = new TcpClient();
                    tcpClient.Connect("localhost", 1338);
                    (TcpClient tcpClient, StreamReader, StreamWriter writer) client = (tcpClient, new StreamReader(tcpClient.GetStream()), new StreamWriter(tcpClient.GetStream()));
                    client.writer.AutoFlush = true;

                    Console.WriteLine($"Writing to team {i}");
                    Console.WriteLine($"Writing to team {TeamStart}");
                    Console.WriteLine($"Writing to team {(i + TeamStart)}");
                    client.writer.Write($"{i + TeamStart}\n");
                    TeamSockets[i + 1][j] = client;
                }
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

            Console.WriteLine("$FlagRunnerLoop starting");

            while (!LauncherCancelSource.IsCancellationRequested)
            {
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                    var flags = await db.RetrieveFlags(FlagCount);
                    var tasks = new Task[TeamCount];
                    if (flags.Length > 0)
                    {
                        Console.WriteLine($"Sending {flags.Length} flags");
                    }
                    for (int i = 0; i < TeamCount; i++)
                    {
                        var ti = i;
                        tasks[ti] = Task.Run(async () => await SendFlagsTask(flags.Select(f => f.ToUtfString(Encoding.UTF8.GetBytes(Configuration.FlagSigningKey))).ToArray(), ti + 1));
                    }
                    await Task.WhenAll(tasks);
                    await Task.Delay(RoundDelay, LauncherCancelSource.Token);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"FlagRunnerLoop retrying because: {EnoDatabaseUtils.FormatException(e)}");
                }
            }
        }

        private async Task SendFlagsTask(string[] flags, long teamId)
        {
            try
            {
                var connections = TeamSockets[teamId];
                var tasks = new List<Task>(SubmissionConnectionsPerTeam);
                for (int i = 0; i < flags.Length; i += SubmissionConnectionsPerTeam)
                {
                    for (int j = 0; j < SubmissionConnectionsPerTeam; j++)
                    {
                        if (i + j < flags.Length)
                        {
                            var con = connections[j];
                            int ti = i;
                            int tj = j;
                            tasks.Add(Task.Run(async () =>
                            {
                                // Console.WriteLine($"Sending flag {flags[ti + tj]}");
                                await con.writer.WriteAsync($"{flags[ti + tj]}\n");
                                Console.WriteLine(await con.reader.ReadLineAsync());
                            }));
                        }
                    }
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"SendFlagTask failed because: {EnoDatabaseUtils.FormatException(e)}");
            }
        }


        static void Main(int flagCount = 10000, int roundDelay = 1000, int teamStart = 1, int teamCount = 10, int teamConnections = 1)
        {
            try
            {
                Console.WriteLine($"FlagShooter starting");
                JsonConfiguration configuration;
                if (!File.Exists("ctf.json"))
                {
                    Console.WriteLine("Config (ctf.json) does not exist");
                    return;
                }
                try
                {
                    var content = File.ReadAllText("ctf.json");
                    configuration = JsonSerializer.Deserialize<JsonConfiguration>(content);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to load ctf.json: {e.Message}");
                    return;
                }

                var serviceProvider = new ServiceCollection()
                    .AddDbContextPool<EnoDatabaseContext>(options =>
                    {
                        options.UseNpgsql(
                            EnoDatabaseUtils.PostgresConnectionString,
                            pgoptions => pgoptions.EnableRetryOnFailure());
                    }, 2)
                    .AddScoped<IEnoDatabase, EnoDatabase.EnoDatabase>()
                    .AddLogging(logging => logging.AddConsole())
                    .BuildServiceProvider(validateScopes: true);
                new Program(serviceProvider, flagCount, roundDelay, teamStart, teamCount, teamConnections, configuration).Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"FlagShooter failed: {EnoDatabaseUtils.FormatException(e)}");
            }
        }
    }
}
