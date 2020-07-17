using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EnoCore;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoCore.Utils;
using EnoDatabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlagShooter
{
    internal class Program
    {
        private static readonly CancellationTokenSource FlagShooterCancelSource = new CancellationTokenSource();
        private static EnoEngineScoreboard? sb;
        private readonly int flagCount;
        private readonly int teamStart;
        private readonly int roundDelay;
        private readonly int teamCount;
        private readonly int submissionConnectionsPerTeam;
        private readonly JsonConfiguration configuration;
        private readonly List<ChannelWriter<byte[]>> flagWriters;

        public Program(int flagCount, int roundDelay, int teamStart, int teamCount, int teamConnections, JsonConfiguration configuration)
        {
            Console.WriteLine($"flagCount {flagCount}, roundDelay {roundDelay}, teamStart {teamStart}, teamCount {teamCount}, teamConnections {teamConnections}");
            this.flagCount = flagCount;
            this.teamStart = teamStart;
            this.roundDelay = roundDelay;
            this.teamCount = teamCount;
            this.submissionConnectionsPerTeam = teamConnections;
            this.configuration = configuration;
            this.flagWriters = new List<ChannelWriter<byte[]>>();
            for (int i = 0; i < this.teamCount; i++)
            {
                for (int j = 0; j < this.submissionConnectionsPerTeam; j++)
                {
                    var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1000)
                    {
                        SingleWriter = true
                    });
                    int localI = i;
                    int localJ = i;
                    Task.Run(async () =>
                    {
                        try
                        {
                            await FlagSubmissionClient.Create(channel.Reader, localI + this.teamStart);
                            Console.WriteLine($"FlagSubmissionClient {localJ} for team {localI + this.teamStart} connected");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to create FlagSubmissionClient {j} for team {localI + this.teamStart}: {e.Message} ({e.GetType()})");
                        }
                    });
                    this.flagWriters.Add(channel.Writer);
                }
            }
        }

        public static void Main(int flagCount = 10000, int roundDelay = 1000, int teamStart = 1, int teamCount = 10, int teamConnections = 1)
        {
            try
            {
                Console.WriteLine($"FlagShooter starting");

                // Console.CancelKeyPress += (s, e) => {
                //     Console.WriteLine("Shutting down FlagShooter");
                //     e.Cancel = true;
                //    FlagShooterCancelSource.Cancel();
                // };
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

                var path = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
                path = Path.Combine(path, "data");
                ParseScoreboard(Path.Combine(path, "scoreboard.json"));
                Task.Run(async () => await PollConfig(path, FlagShooterCancelSource.Token));
                new Program(flagCount, roundDelay, teamStart, teamCount, teamConnections, configuration).Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"FlagShooter failed: {EnoDatabaseUtils.FormatException(e)}");
            }
        }

        public void Start() => this.FlagRunnerLoop().Wait(FlagShooterCancelSource.Token);

        public List<List<Task>> BeginSubmitFlags(long flagCount)
        {
            var result = new List<List<Task>>();
            try
            {
                long i = 0;
                if (sb != null)
                {
                    if (sb.CurrentRound != null && sb.CurrentRound > 0)
                    {
                        for (long r = sb.CurrentRound.Value; r > Math.Max(sb.CurrentRound.Value - this.configuration.FlagValidityInRounds, 1); r--)
                        {
                            for (int team = 0; team < this.configuration.Teams.Count; team++)
                            {
                                foreach (var s in sb.Services)
                                {
                                    for (int store = 0; store < s.MaxStores; store++)
                                    {
                                        if (i++ > flagCount)
                                        {
                                            return result;
                                        }

                                        result.Add(this.SubmitFlag(new Flag()
                                        {
                                            RoundId = r,
                                            OwnerId = team,
                                            ServiceId = s.ServiceId,
                                            RoundOffset = store
                                        }));
                                    }
                                }
                            }
                        }

                        Console.WriteLine($"Not Enough Flags available, requested {flagCount} and got {i}");
                        return result;
                    }
                }

                Console.WriteLine($"No Flags could be generated, Scoreboard data not Found");
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception Generating flags: {e.ToFancyString()}");
                return result;
            }
        }

        public async Task FlagRunnerLoop()
        {
            Console.WriteLine($"FlagRunnerLoop starting");
            while (!FlagShooterCancelSource.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"Next batch of flags, current round {sb?.CurrentRound}");
                    var taskLists = this.BeginSubmitFlags(this.flagCount);
                    foreach (var taskList in taskLists)
                    {
                        await Task.WhenAll(taskList);
                    }

                    await Task.Delay(this.roundDelay, FlagShooterCancelSource.Token);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"FlagRunnerLoop retrying because: {EnoDatabaseUtils.FormatException(e)}");
                }
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
            if (e.Name.Contains("scoreboard.json"))
            {
                ParseScoreboard(e.FullPath);
            }
        }

        private static async Task PollConfig(string path, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ParseScoreboard(Path.Combine(path, "scoreboard.json"));
                await Task.Delay(1000);
            }
        }

        private List<Task> SubmitFlag(Flag f)
        {
            var tasks = new List<Task>();
            foreach (var writer in this.flagWriters)
            {
                tasks.Add(Task.Run(async () => await writer.WriteAsync(Encoding.UTF8.GetBytes(f.ToString(Encoding.UTF8.GetBytes(this.configuration.FlagSigningKey), this.configuration.Encoding) + "\n"))));
            }

            return tasks;
        }
    }
}
