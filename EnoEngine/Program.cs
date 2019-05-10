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
using System.Diagnostics;

namespace EnoEngine
{
    class Program
    {
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoEngine));
        private static readonly CancellationTokenSource EngineCancelSource = new CancellationTokenSource();
        public static JsonConfiguration Configuration { get; set; }
        CTF EnoGame;

        public int Start()
        {
            // Gracefully shutdown when CTRL+C is invoked
            Console.CancelKeyPress += (s, e) => {
                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(EnoEngine),
                    Function = nameof(Start),
                    Message = "Shutting down EnoEngine"
                });
                e.Cancel = true;
                EngineCancelSource.Cancel();
            };
            if (!File.Exists("ctf.json"))
            {
                Logger.LogFatal(new EnoLogMessage()
                {
                    Module = nameof(EnoEngine),
                    Function = nameof(Start),
                    Message = "Config (ctf.json) didn't exist. Creating sample and exiting"
                });
                CreateConfig();
                return 1;
            }
            var content = File.ReadAllText("ctf.json");
            Configuration = JsonConvert.DeserializeObject<JsonConfiguration>(content);
            var result = EnoDatabase.ApplyConfig(Configuration, Logger);
            if (result.Success)
            {
                GameLoop().Wait();
                return 0;
            }
            else
            {
                Logger.LogFatal(new EnoLogMessage()
                {
                    Module = nameof(EnoEngine),
                    Function = nameof(Start),
                    Message = $"Invalid configuration, exiting ({result.ErrorMessage}"
                });
                return 1;
            }
        }

        internal async Task GameLoop()
        {
            try
            {
                EnoGame = new CTF(EngineCancelSource.Token);
                while (!EngineCancelSource.IsCancellationRequested)
                {
                    var end = await EnoGame.StartNewRound();
                    await EnoCoreUtils.DelayUntil(end, EngineCancelSource.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Logger.LogError(new EnoLogMessage()
                {
                    Module = nameof(EnoEngine),
                    Function = nameof(GameLoop),
                    Message = $"GameLoop failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
            Logger.LogInfo(new EnoLogMessage()
            {
                Module = nameof(EnoEngine),
                Function = nameof(GameLoop),
                Message = "GameLoop finished"
            });
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

        public static void Main(string[] args)
        {
            Logger.LogInfo(new EnoLogMessage()
            {
                Module = nameof(EnoEngine),
                Function = nameof(Main),
                Message = "EnoEngine starting"
            });
            new Program().Start();
        }
    }
}