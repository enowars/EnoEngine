using EnoCore;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnoLauncher
{
    class Program
    {
        private const int TASK_UPDATE_BATCH_SIZE = 500;
        private static readonly ConcurrentQueue<CheckerTask> ResultsQueue = new ConcurrentQueue<CheckerTask>();
        private static readonly CancellationTokenSource LauncherCancelSource = new CancellationTokenSource();
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoLauncher));
        private static readonly HttpClient Client = new HttpClient();
        private readonly Task UpdateDatabaseTask;
        private readonly ServiceProvider ServiceProvider;

        public Program(ServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }
            UpdateDatabaseTask = Task.Run(async () => await UpdateDatabaseLoop());
        }

        public void Start()
        {
                Client.Timeout = new TimeSpan(0, 1, 0);
            LauncherLoop().Wait();
        }

        public async Task LauncherLoop()
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(EnoLauncher),
                    Function = nameof(LauncherLoop),
                    Message = $"LauncherLoop starting"
                });

                while (!LauncherCancelSource.IsCancellationRequested)
                {
                    
                    var tasks = await db.RetrievePendingCheckerTasks(100);
                    if (tasks.Count > 0)
                    {
                        Logger.LogInfo(new EnoLogMessage()
                        {
                            Module = nameof(EnoLauncher),
                            Function = nameof(LauncherLoop),
                            Message = $"Scheduling {tasks.Count} tasks"
                        });
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
            }
        }

        public async Task LaunchCheckerTask(CheckerTask task)
        {
            try
            {
                var message = EnoLogMessage.FromCheckerTask(task);
                message.Module = nameof(EnoLauncher);
                message.Function = nameof(LaunchCheckerTask);
                message.Message = $"LaunchCheckerTask for task {task.Id} ({task.TaskType}, currentRound={task.CurrentRoundId}, relatedRound={task.RelatedRoundId})";
                Logger.LogTrace(message);
                var cancelSource = new CancellationTokenSource();
                var now = DateTime.UtcNow;
                var span = task.StartTime.Subtract(DateTime.UtcNow);
                if (span.TotalSeconds < -0.5)
                {
                    message.Message = $"Task starts {span.TotalSeconds} late";
                    Logger.LogWarning(message);
                }
                if (task.StartTime > now)
                {
                    await Task.Delay(span);
                }
                var content = new StringContent(JsonConvert.SerializeObject(task), Encoding.UTF8, "application/json");
                message.Message = $"LaunchCheckerTask {task.Id} POSTing {task.TaskType} to checker";
                Logger.LogTrace(message);
                cancelSource.CancelAfter(task.MaxRunningTime * 1000);
                var response = await Client.PostAsync(new Uri(task.CheckerUrl), content, cancelSource.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var responseString = (await response.Content.ReadAsStringAsync()).TrimEnd(Environment.NewLine.ToCharArray());
                    message.Message = $"LaunchCheckerTask received {responseString}";
                    Logger.LogTrace(message);
                    dynamic responseJson = JsonConvert.DeserializeObject(responseString);
                    string result = responseJson.result;
                    var checkerResult = EnoCoreUtils.ParseCheckerResult(result);
                    message.Message = $"LaunchCheckerTask {task.Id} returned {checkerResult}";
                    Logger.LogTrace(message);
                    task.CheckerResult = checkerResult;
                    task.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done;
                    ResultsQueue.Enqueue(task);
                }
                else
                {
                    message.Message = $"LaunchCheckerTask {task.Id} returned {response.StatusCode} ({(int) response.StatusCode})";
                    Logger.LogError(message);
                    task.CheckerResult = CheckerResult.CheckerError;
                    task.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done;
                    ResultsQueue.Enqueue(task);
                }
            }
            catch (TaskCanceledException e)
            {
                var message = EnoLogMessage.FromCheckerTask(task);
                message.Module = nameof(EnoLauncher);
                message.Function = nameof(LaunchCheckerTask);
                message.Message = $"{nameof(LaunchCheckerTask)} {task.Id} was cancelled: {EnoCoreUtils.FormatException(e)}";
                Logger.LogTrace(message);
                task.CheckerResult = CheckerResult.Down;
                task.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done;
                ResultsQueue.Enqueue(task);
            }
            catch (Exception e)
            {
                var message = EnoLogMessage.FromCheckerTask(task);
                message.Module = nameof(EnoLauncher);
                message.Function = nameof(LaunchCheckerTask);
                message.Message = $"{nameof(LaunchCheckerTask)} failed: {EnoCoreUtils.FormatException(e)}";
                Logger.LogError(message);
                task.CheckerResult = CheckerResult.CheckerError;
                task.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done;
                ResultsQueue.Enqueue(task);
            }
        }

        static void Main(string[] args)
        {
            try
            {
                Logger.LogInfo(new EnoLogMessage()
                {
                    Module = nameof(EnoLauncher),
                    Function = nameof(Main),
                    Message = $"EnoLauncher starting"
                });
                var serviceProvider = new ServiceCollection()
                    .AddDbContextPool<EnoDatabaseContext>(options => {
                        options.UseNpgsql(
                            EnoCoreUtils.PostgresConnectionString,
                            pgoptions => pgoptions.EnableRetryOnFailure());
                    }, 2)
                    .AddScoped<IEnoDatabase, EnoDatabase>()
                    .BuildServiceProvider(validateScopes: true);
                new Program(serviceProvider).Start();
            }
            catch (Exception e)
            {
                Logger.LogFatal(new EnoLogMessage()
                {
                    Module = nameof(EnoLauncher),
                    Function = nameof(Main),
                    Message = $"EnoLauncher failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
        }

        async Task UpdateDatabaseLoop()
        {
            try
            {
                using (var scope = ServiceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
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
                        await db.UpdateTaskCheckerTaskResults(results.AsMemory(0, i));
                        if (i != TASK_UPDATE_BATCH_SIZE)
                        {
                            await Task.Delay(1);
                        }
                    }
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogFatal(new EnoLogMessage()
                {
                    Module = nameof(EnoLauncher),
                    Function = nameof(UpdateDatabaseLoop),
                    Message = $"UpdateDatabase failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
        }
    }
}
