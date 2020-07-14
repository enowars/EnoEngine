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
using EnoCore.Utils;

namespace FlagShooter
{
    class Program
    {
        private static readonly CancellationTokenSource LauncherCancelSource = new CancellationTokenSource();
        private readonly Dictionary<long, (TcpClient, StreamReader reader, StreamWriter writer)[]> TeamSockets =
            new Dictionary<long, (TcpClient, StreamReader reader, StreamWriter writer)[]>();
        private readonly int FlagCount;
        private readonly int TeamStart;
        private readonly int RoundDelay;
        private readonly int TeamCount;
        private readonly int SubmissionConnectionsPerTeam;
        private readonly JsonConfiguration Configuration;
        private static FileSystemWatcher Watch;
        private static EnoEngineScoreboard sb;

        public Program(int flagCount, int roundDelay, int teamStart, int teamCount, int teamConnections, JsonConfiguration configuration)
        {
            FlagCount = flagCount;
            TeamStart = teamStart;
            RoundDelay = roundDelay;
            TeamCount = teamCount;
            SubmissionConnectionsPerTeam = teamConnections;
            Configuration = configuration;
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
        public List<Flag> generateFlags(long FlagCount)
        {
            try
            {
                long i = 0;
                var result = new List<Flag>();
                if (sb != null)
                    if (sb.CurrentRound != null)
                    {
                        for (long r = sb.CurrentRound.Value; r > Math.Max(sb.CurrentRound.Value - Configuration.FlagValidityInRounds, 1); r--)
                            for (int team = 0; team < Configuration.Teams.Count; team++)
                                foreach (var s in sb.Services)
                                    for (int store = 0; store < s.MaxStores; store++)
                                    {
                                        if (i++ > FlagCount) return result;
                                        result.Add(new Flag()
                                        {
                                            RoundId = (sb.CurrentRound - r) ?? 0,
                                            OwnerId = team,
                                            ServiceId = s.ServiceId,
                                            RoundOffset = store
                                        });
                                    }

                        Console.WriteLine($"Not Enough Flags available, requested {FlagCount} and got {i}");
                        return result;
                    }
                Console.WriteLine($"No Flags could be generated, Scoreboard data not Found");
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception Generating flags: {e.ToFancyString()}");
                throw e;
            }
        }

        public async Task FlagRunnerLoop()
        {
            Console.WriteLine("$FlagRunnerLoop starting");

            while (!LauncherCancelSource.IsCancellationRequested)
            {
                try
                {
                    var flags = generateFlags(FlagCount);
                    var tasks = new Task[TeamCount];
                    if (flags.Count > 0)
                    {
                        Console.WriteLine($"Sending {flags.Count} flags");

                        for (int i = 0; i < TeamCount; i++)
                        {
                            var ti = i;
                            tasks[ti] = Task.Run(async () => await SendFlagsTask(flags.Select(f => f.ToString(Encoding.UTF8.GetBytes(Configuration.FlagSigningKey), Configuration.Encoding)).ToArray(), ti + 1));
                        }
                        await Task.WhenAll(tasks);
                        await Task.Delay(RoundDelay, LauncherCancelSource.Token);
                    }
                    else
                    {
                        await Task.Delay(1000);
                    }
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
        private static void ParseScoreboard(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Console.WriteLine($"Config {fileName} does not exist");
                return;
            }
            try
            {
                var content = File.ReadAllText(fileName);
                sb = JsonSerializer.Deserialize<EnoEngineScoreboard>(content);

            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load {fileName}: {e.Message}");
                return;
            }
        }
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
            if (e.Name.Contains("scoreboard.json")) ParseScoreboard(e.FullPath);
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
                Watch = new FileSystemWatcher()
                {
                    Path = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, "data"),
                    NotifyFilter =  NotifyFilters.LastWrite |
                                    NotifyFilters.LastAccess |
                                    NotifyFilters.FileName,
                };
                Watch.Filters.Add("scoreboard.json");
                //Watch.Filters.Add("scoreboardInfo.json");
                Watch.Changed += OnChanged;
                Watch.Created += OnChanged;
                Watch.Deleted += OnChanged;

                new Program(flagCount, roundDelay, teamStart, teamCount, teamConnections, configuration).Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"FlagShooter failed: {EnoDatabaseUtils.FormatException(e)}");
            }
        }
    }
}
