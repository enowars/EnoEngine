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
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EnoEngine
{
    class Program
    {
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoEngine));
        private static readonly CancellationTokenSource EngineCancelSource = new CancellationTokenSource();
        public static JsonConfiguration Configuration { get; set; }

        readonly CTF EnoGame;
        readonly ServiceProvider ServiceProvider;

        public Program(ServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            EnoGame = new CTF(serviceProvider, EngineCancelSource.Token);
        }

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
            var db = ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<IEnoDatabase>();
            var result = db.ApplyConfig(Configuration);
            Configuration.BuildCheckersDict();
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
                    Message = $"Invalid configuration, exiting ({result.ErrorMessage})"
                });
                return 1;
            }
        }

        internal async Task GameLoop()
        {
            try
            {
                //Check if there is an old round running
                await AwaitOldRound();
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

        private async Task AwaitOldRound()
        {
            var lastRound = await EnoCoreUtils.RetryScopedDatabaseAction(ServiceProvider,
                    async (IEnoDatabase db) => await db.GetLastRound());

            if (lastRound != null)
            {
                Logger.LogInfo(new EnoLogMessage()
                {
                    Message = $"Sleeping until old round ends ({lastRound.End})",
                    RoundId = lastRound.Id
                });
                var span = lastRound.End.Subtract(DateTime.UtcNow);
                await Task.Delay(span);
            }
        }

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Async(a => a.File("../data/engine.log",
                    outputTemplate: "{Message}{NewLine}"))
                .CreateLogger();

            Logger.LogInfo(new EnoLogMessage()
            {
                Module = nameof(EnoEngine),
                Function = nameof(Main),
                Message = "EnoEngine starting"
            });
            var serviceProvider = new ServiceCollection()
                .AddDbContextPool<EnoDatabaseContext>(options => {
                    options.UseNpgsql(
                        EnoCoreUtils.PostgresConnectionString,
                        pgoptions => pgoptions.EnableRetryOnFailure());
                }, 90)
                .AddScoped<IEnoDatabase, EnoDatabase>()
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddSerilog(dispose: true);
                    loggingBuilder.AddFilter((category, level) => category != DbLoggerCategory.Database.Command.Name);
                })
                .BuildServiceProvider(validateScopes: true);
            new Program(serviceProvider).Start();
        }

        private static ILoggerFactory GetLoggerFactory()
        {
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder =>
                builder.AddConsole());
            return serviceCollection.BuildServiceProvider()
                .GetService<ILoggerFactory>();
        }
    }
}