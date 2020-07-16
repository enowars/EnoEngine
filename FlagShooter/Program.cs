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
using System.Threading.Channels;

namespace FlagShooter
{
    class Program
    {
        private static readonly CancellationTokenSource FlagShooterCancelSource = new CancellationTokenSource();
        private readonly int FlagCount;
        private readonly int TeamStart;
        private readonly int RoundDelay;
        private readonly int TeamCount;
        private readonly int SubmissionConnectionsPerTeam;
        private readonly JsonConfiguration Configuration;
        private readonly List<ChannelWriter<byte[]>> FlagWriters;

        //private static FileSystemWatcher Watch;
        private static EnoEngineScoreboard? sb;

        public Program(int flagCount, int roundDelay, int teamStart, int teamCount, int teamConnections, JsonConfiguration configuration)
        {
            FlagCount = flagCount;
            TeamStart = teamStart;
            RoundDelay = roundDelay;
            TeamCount = teamCount;
            SubmissionConnectionsPerTeam = teamConnections;
            Configuration = configuration;
            FlagWriters = new List<ChannelWriter<byte[]>>();
            for (int i = 0; i < TeamCount; i++)
            {
                for (int j = 0; j < SubmissionConnectionsPerTeam; j++)
                {
                    var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1000)
                    {
                        SingleWriter = true
                    });
                    Task.Run(async () =>
                    {
                        try
                        {
                            await FlagSubmissionClient.Create(channel.Reader, i + TeamStart);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to create FlagSubmissionClient {j} for team {i + TeamStart}: {e.Message} ({e.GetType()})");
                        }
                    });
                    FlagWriters.Add(channel.Writer);
                }
            }
        }

        private List<Task> SubmitFlag(Flag f)
        {
            var tasks = new List<Task>();
            foreach (var writer in FlagWriters)
            {
                tasks.Add(Task.Run(async () => await writer.WriteAsync(Encoding.UTF8.GetBytes(f.ToString(Encoding.UTF8.GetBytes(Configuration.FlagSigningKey), Configuration.Encoding) + "\n"))));
            }
            return tasks;
        }

        public void Start()
        {
            FlagRunnerLoop().Wait();
        }
        public List<List<Task>> BeginSubmitFlags(long FlagCount)
        {
            var result = new List<List<Task>>();
            try
            {
                long i = 0;
                if (sb != null)
                    if (sb.CurrentRound != null && sb.CurrentRound >0)
                    {
                        for (long r = sb.CurrentRound.Value; r > Math.Max(sb.CurrentRound.Value - Configuration.FlagValidityInRounds, 1); r--)
                            for (int team = 0; team < Configuration.Teams.Count; team++)
                                foreach (var s in sb.Services)
                                    for (int store = 0; store < s.MaxStores; store++)
                                    {
                                        if (i++ > FlagCount) return result;

                                        result.Add(SubmitFlag(new Flag()
                                        {
                                            RoundId = r,
                                            OwnerId = team,
                                            ServiceId = s.ServiceId,
                                            RoundOffset = store
                                        }));
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
                return result;
                //throw e;
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
                    var taskLists = BeginSubmitFlags(FlagCount);
                    foreach (var taskList in taskLists)
                        await Task.WhenAll(taskList);
                    await Task.Delay(RoundDelay, FlagShooterCancelSource.Token);
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
            if (e.Name.Contains("scoreboard.json")) ParseScoreboard(e.FullPath);
        }
        private async static Task PollConfig(string path, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ParseScoreboard(Path.Combine(path + "scoreboard.json"));
                await Task.Delay(1000);
            }
        }
        static void Main(int flagCount = 10000, int roundDelay = 1000, int teamStart = 1, int teamCount = 10, int teamConnections = 1)
        {
            try
            {
                Console.WriteLine($"FlagShooter starting");
                //Console.CancelKeyPress += (s, e) => {
                //    Console.WriteLine("Shutting down FlagShooter");
                //    e.Cancel = true;
                //    FlagShooterCancelSource.Cancel();
                //};
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
                Console.WriteLine($"Path is: {path}");
                path = Path.Combine(path, "data");
                Console.WriteLine($"Path is: {path}");
                ParseScoreboard(Path.Combine(path, "scoreboard.json"));
                Task.Run(async () => await PollConfig(path, FlagShooterCancelSource.Token));

                new Program(flagCount, roundDelay, teamStart, teamCount, teamConnections, configuration).Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"FlagShooter failed: {EnoDatabaseUtils.FormatException(e)}");
            }
        }
    }
}
