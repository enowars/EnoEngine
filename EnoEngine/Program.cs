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

namespace EnoEngine
{
    class Program
    {
        readonly CancellationTokenSource EngineCancelSource = new CancellationTokenSource();
        public static JsonConfiguration Configuration { get; set; }
        Task GameLoopTask;
        CTF EnoGame;

        internal void Run()
        {
            GameLoopTask = GameLoop();
            GameLoopTask.Wait();
        }

        internal void Shutdown()
        {
            Console.WriteLine($"Shutting down EnoEngine");
            EngineCancelSource.Cancel();
            GameLoopTask.Wait();
            Console.WriteLine($"EnoEngine has shut down");
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
                Console.WriteLine($"GameLoop failed: {EnoCoreUtils.FormatException(e)}");
            }
            Console.WriteLine("GameLoop finished");
        }

        static int Main(string[] args)
        {
            if (!File.Exists("ctf.json"))
            {
                Console.WriteLine("Config (ctf.json) didn't exist. Creating...");
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
                Console.WriteLine(result.ErrorMessage);
                Console.WriteLine($"Invalid configuration, exiting");
                return 1;
            }
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
    }
}