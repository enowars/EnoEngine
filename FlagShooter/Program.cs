﻿namespace FlagShooter
{
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
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Configuration;
    using EnoCore.Models;
    using EnoCore.Scoreboard;
    using EnoDatabase;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    internal class Program
    {
        private static readonly CancellationTokenSource FlagShooterCancelSource = new CancellationTokenSource();
        private static Scoreboard? sb;
        private readonly int flagCount;
        private readonly int teamStart;
        private readonly int roundDelay;
        private readonly int teamCount;
        private readonly int submissionConnectionsPerTeam;
        private readonly Configuration configuration;
        private readonly List<ChannelWriter<byte[]>> flagWriters;

        public Program(int flagCount, int roundDelay, int teamStart, int teamCount, int teamConnections, Configuration configuration)
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
                        SingleWriter = true,
                    });
                    int localI = i;
                    int localJ = j;
                    Task.Run(async () =>
                    {
                        try
                        {
                            await FlagSubmissionClient.Create(channel.Reader, localI + this.teamStart);
                            Console.WriteLine($"FlagSubmissionClient {localJ} for team {localI + this.teamStart} connected");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to create FlagSubmissionClient {localJ} for team {localI + this.teamStart}: {e.Message} ({e.GetType()})");
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

                if (!File.Exists("ctf.json"))
                {
                    Console.WriteLine("Config (ctf.json) does not exist");
                    return;
                }

                // Check if config is valid
                Configuration configuration;
                try
                {
                    var content = File.ReadAllText("ctf.json");
                    var jsonConfiguration = JsonConfiguration.Deserialize(content);
                    if (jsonConfiguration is null)
                    {
                        Console.WriteLine("Deserialization of config failed.");
                        return;
                    }

                    configuration = Task.Run(() => jsonConfiguration.ValidateAsync()).Result;
                }
                catch (JsonException e)
                {
                    Console.WriteLine($"Configuration could not be deserialized: {e.Message}");
                    return;
                }
                catch (JsonConfigurationValidationException e)
                {
                    Console.WriteLine($"Configuration is invalid: {e.Message}");
                    return;
                }

                ParseScoreboard(Path.Combine(EnoCoreUtil.DataDirectory, "scoreboard.json"));
                Task.Run(async () => await PollConfig(EnoCoreUtil.DataDirectory, FlagShooterCancelSource.Token));
                new Program(flagCount, roundDelay, teamStart, teamCount, teamConnections, configuration).Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"FlagShooter failed: {e.ToFancyStringWithCaller()}");
            }
        }

        public void Start() => this.FlagRunnerLoop().Wait(FlagShooterCancelSource.Token);

        public List<List<Task>> BeginSubmitFlags(long flagCount)
        {
            var result = new List<List<Task>>();
            try
            {
                long i = 0;
                if (sb != null && sb.CurrentRound > 0)
                {
                    for (long r = sb.CurrentRound; r > Math.Max(sb.CurrentRound - this.configuration.FlagValidityInRounds, 0); r--)
                    {
                        for (int team = 0; team < this.configuration.Teams.Count; team++)
                        {
                            foreach (var s in sb.Services)
                            {
                                for (int store = 0; store < s.FlagVariants; store++)
                                {
                                    if (i++ > flagCount)
                                    {
                                        return result;
                                    }

                                    result.Add(this.SubmitFlag(new Flag(team, s.ServiceId, store, r, 0)));
                                }
                            }
                        }
                    }

                    Console.WriteLine($"Not Enough Flags available, requested {flagCount} and got {i}");
                    return result;
                }

                Console.WriteLine($"No Flags could be generated, Scoreboard data not Found");
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception Generating flags: {e.ToFancyStringWithCaller()}");
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
                    Console.WriteLine($"FlagRunnerLoop retrying because: {e.ToFancyStringWithCaller()}");
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
                sb = JsonSerializer.Deserialize<Scoreboard>(content, new JsonSerializerOptions()
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter() },
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load {fileName}: {e.Message}");
                return;
            }
        }

        private static async Task PollConfig(string path, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ParseScoreboard(Path.Combine(path, "scoreboard.json"));
                await Task.Delay(1000, token);
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
