using EnoEngine.FlagSubmission;
using EnoEngine.Game;
using EnoCore.Models;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EnoCore;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.DependencyInjection;

namespace EnoEngine
{
    class Program
    {
        private static readonly ILogger Logger = EnoCoreUtils.Loggers.CreateLogger<Program>();
        readonly CancellationTokenSource EngineCancelSource = new CancellationTokenSource();
        public static JsonConfiguration Configuration { get; set; }
        Task GameLoopTask;
        CTF EnoGame;

        public int Start()
        {
            if (!File.Exists("ctf.json"))
            {
                Logger.LogCritical("Config (ctf.json) didn't exist. Creating...");
                CreateConfig();
                return 1;
            }
            var content = File.ReadAllText("ctf.json");
            Configuration = JsonConvert.DeserializeObject<JsonConfiguration>(content);
            var result = EnoDatabase.ApplyConfig(Configuration);
            if (result.Success)
            {
                new Program().Run();
                return 0;
            }
            else
            {
                Logger.LogCritical(result.ErrorMessage);
                Logger.LogCritical($"Invalid configuration, exiting");
                return 1;
            }
        }

        internal void Run()
        {
            GameLoopTask = GameLoop();
            GameLoopTask.Wait();
        }

        internal void Shutdown()
        {
            Logger.LogInformation($"Shutting down EnoEngine");
            EngineCancelSource.Cancel();
            GameLoopTask.Wait();
            Logger.LogTrace($"EnoEngine has shut down");
        }

        internal async Task GameLoop()
        {
            try
            {
                EnoGame = new CTF(EngineCancelSource.Token);
                while (!EngineCancelSource.IsCancellationRequested)
                {
                    await EnoGame.StartNewRound();
                    await Task.Delay(Configuration.RoundLengthInSeconds * 1000, EngineCancelSource.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Logger.LogError($"GameLoop failed: {EnoCoreUtils.FormatException(e)}");
            }
            Logger.LogTrace("GameLoop finished");
        }

        private static void CreateConfig()
        {
            using (FileStream fs = File.Open("ctf.json", FileMode.Create))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    using (JsonTextWriter jw = new JsonTextWriter(sw))
                    {
                        jw.Formatting = Formatting.Indented;
                        jw.IndentChar = ' ';
                        jw.Indentation = 4;
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jw, new JsonConfiguration());
                    }
                }
            }
        }

        static int Main(string[] args)
        {
            EnoCoreUtils.InitLogging();
            return new Program().Start();
        }
    }
}