using EnoCore;
using EnoCore.Logging;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using EnoCore.Utils.LoggerExtensions;
using EnoDatabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

namespace EnoLauncher
{
    class Program
    {
        private const int TASK_UPDATE_BATCH_SIZE = 500;
        private const int MAX_RETRIES = 1;
        private static readonly ConcurrentQueue<CheckerTask> ResultsQueue = new ConcurrentQueue<CheckerTask>();
        private static readonly CancellationTokenSource LauncherCancelSource = new CancellationTokenSource();
        private static readonly HttpClient Client = new HttpClient();
        private readonly Task UpdateDatabaseTask;
        private readonly ServiceProvider ServiceProvider;
        private readonly EnoStatistics Statistics;
        private readonly ILogger Logger;

        public Program(ServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Statistics = new EnoStatistics(nameof(EnoLauncher));
            UpdateDatabaseTask = Task.Run(async () => await UpdateDatabaseLoop());
            Logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        }

        public void Start()
        {
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

            Client.Timeout = new TimeSpan(0, 1, 0);
            LauncherLoop().Wait();
        }

        public async Task LauncherLoop()
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }

            Logger.LogInformation($"LauncherLoop starting");
            while (!LauncherCancelSource.IsCancellationRequested)
            {
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                    var tasks = await db.RetrievePendingCheckerTasks(1000);
                    if (tasks.Count > 0)
                    {
                        Logger.LogDebug($"Scheduling {tasks.Count} tasks");
                    }
                    foreach (var task in tasks)
                    {
                        var t = Task.Run(async () => await LaunchCheckerTask(task));
                    }
                    if (tasks.Count == 0)
                    {
                        await Task.Delay(50, LauncherCancelSource.Token);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"LauncherLoop retrying because: {EnoDatabaseUtils.FormatException(e)}");
                }
            }
        }

        public async Task LaunchCheckerTask(CheckerTask task)
        {
            using var scope = Logger.BeginEnoScope(task);
            try
            {
                Logger.LogTrace($"LaunchCheckerTask() for task {task.Id} ({task.Method}, currentRound={task.CurrentRoundId}, relatedRound={task.RelatedRoundId})");
                var cancelSource = new CancellationTokenSource();
                var now = DateTime.UtcNow;
                var span = task.StartTime.Subtract(DateTime.UtcNow);
                if (span.TotalSeconds < -0.5)
                {
                    Logger.LogWarning($"Task {task.Id} starts {span.TotalSeconds} late (should: {task.StartTime})");
                }
                if (task.StartTime > now)
                {
                    Logger.LogTrace($"Task {task.Id} sleeping: {span}");
                    await Task.Delay(span);
                }
                var content = new StringContent(JsonSerializer.Serialize(new CheckerTaskMessage(task)), Encoding.UTF8, "application/json");
                cancelSource.CancelAfter(task.MaxRunningTime * 1000);
                Statistics.CheckerTaskLaunchMessage(task);
                Logger.LogDebug($"LaunchCheckerTask {task.Id} POSTing {task.Method} to checker");
                var response = await Client.PostAsync(new Uri(task.CheckerUrl), content, cancelSource.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var responseString = (await response.Content.ReadAsStringAsync()).TrimEnd(Environment.NewLine.ToCharArray());
                    Logger.LogDebug($"LaunchCheckerTask received {responseString}");
                    var resultMessage = JsonSerializer.Deserialize<CheckerResultMessage>(responseString);
                    var checkerResult = resultMessage.Result;
                    Logger.LogDebug($"LaunchCheckerTask {task.Id} returned {checkerResult} with Message {resultMessage.Message}");
                    task.CheckerResult = checkerResult;
                    task.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done;
                    ResultsQueue.Enqueue(task);
                    return;
                }
                else
                {
                    Logger.LogError($"LaunchCheckerTask {task.Id} returned {response.StatusCode} ({(int)response.StatusCode})");
                    task.CheckerResult = CheckerResult.INTERNAL_ERROR;
                    task.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done;
                    ResultsQueue.Enqueue(task);
                    return;
                }
            }
            catch (TaskCanceledException e)
            {
                Logger.LogError($"{nameof(LaunchCheckerTask)} {task.Id} was cancelled because it did not finish: {EnoDatabaseUtils.FormatException(e)}");
                task.CheckerResult = CheckerResult.OFFLINE;
                task.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done;
                ResultsQueue.Enqueue(task);
            }
            catch (Exception e)
            {
                Logger.LogError($"{nameof(LaunchCheckerTask)} failed: {EnoDatabaseUtils.FormatException(e)}");
                task.CheckerResult = CheckerResult.INTERNAL_ERROR;
                task.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done;
                ResultsQueue.Enqueue(task);
            }
        }

        static void Main()
        {
            const string mutexId = @"Global\EnoLauncher";
            using var mutex = new Mutex(false, mutexId, out var _);
            try
            {
                if (mutex.WaitOne(10, false))
                {
                    var serviceProvider = new ServiceCollection()
                        .AddScoped<IEnoDatabase, EnoDatabase.EnoDatabase>()
                        .AddDbContextPool<EnoDatabaseContext>(options =>
                        {
                            options.UseNpgsql(
                                EnoDatabaseUtils.PostgresConnectionString,
                                pgoptions => pgoptions.EnableRetryOnFailure());
                        }, 90)
                        .AddLogging(loggingBuilder =>
                        {
                            loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                            loggingBuilder.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
                            loggingBuilder.AddConsole();
                            loggingBuilder.AddProvider(new EnoLogMessageFileLoggerProvider("EnoLauncher", LauncherCancelSource.Token));
                        })
                        .BuildServiceProvider(validateScopes: true);
                    new Program(serviceProvider).Start();
                }
                else
                {
                    Console.WriteLine("Another Instance is already running");
                }


            }
            finally
            {
                mutex?.Close();
            }
        }

        async Task UpdateDatabaseLoop()
        {
            try
            {
                while (!LauncherCancelSource.IsCancellationRequested)
                {
                    CheckerTask[] results = new CheckerTask[TASK_UPDATE_BATCH_SIZE];
                    int i = 0;
                    while (i < TASK_UPDATE_BATCH_SIZE)
                    {
                        if (ResultsQueue.TryDequeue(out var result))
                        {
                            results[i] = result;
                            i += 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    while (!LauncherCancelSource.IsCancellationRequested)
                    {
                        try
                        {
                            using (var scope = ServiceProvider.CreateScope())
                            {
                                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                                await db.UpdateTaskCheckerTaskResults(results.AsMemory(0, i));
                            }
                            if (i != TASK_UPDATE_BATCH_SIZE)
                            {
                                await Task.Delay(1);
                            }
                            break;
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception e)
                        {
                            Logger.LogInformation($"UpdateDatabase retrying because: {EnoDatabaseUtils.FormatException(e)}");
                        }
                    }
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogCritical($"UpdateDatabase failed: {EnoDatabaseUtils.FormatException(e)}");
            }
        }
    }
}
