namespace EnoLauncher
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Logging;
    using EnoCore.Models;
    using EnoCore.Models.CheckerApi;
    using EnoCore.Models.Database;
    using EnoDatabase;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public sealed class Program : IDisposable
    {
        private const int TaskUpdateBatchSize = 500;
        private const int LauncherThreads = 1;
        private const int MaxRetries = 1;
        private static readonly ConcurrentQueue<CheckerTask> ResultsQueue = new ConcurrentQueue<CheckerTask>();
        private static readonly CancellationTokenSource LauncherCancelSource = new CancellationTokenSource();
        private static readonly HttpClient Client = new HttpClient();
        private readonly Task updateDatabaseTask;
        private readonly ServiceProvider serviceProvider;
        private readonly EnoStatistics statistics;
        private readonly ILogger logger;

        public Program(ServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.statistics = new EnoStatistics(nameof(EnoLauncher));
            this.updateDatabaseTask = Task.Run(async () => await this.UpdateDatabaseLoop());
            this.logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        }

        public void Dispose()
        {
            this.statistics.Dispose();
        }

        public void Start()
        {
            Client.Timeout = new TimeSpan(0, 1, 0);
            var loops = new Task[LauncherThreads];
            for (int i = 0; i < LauncherThreads; i++)
            {
                loops[i] = this.LauncherLoop();
            }

            Task.WaitAll(loops);
        }

        public async Task LauncherLoop()
        {
            using (var scope = this.serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                db.Migrate();
            }

            this.logger.LogInformation($"LauncherLoop starting");
            while (!LauncherCancelSource.IsCancellationRequested)
            {
                try
                {
                    using var scope = this.serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                    var tasks = await db.RetrievePendingCheckerTasks(500);
                    if (tasks.Count > 0)
                    {
                        this.logger.LogDebug($"Scheduling {tasks.Count} tasks");
                    }

                    foreach (var task in tasks)
                    {
                        var t = Task.Run(async () => await this.LaunchCheckerTask(task));
                    }

                    if (tasks.Count == 0)
                    {
                        await Task.Delay(50, LauncherCancelSource.Token);
                    }
                }
                catch (Exception e)
                {
                    this.logger.LogWarning($"LauncherLoop retrying because: {e.ToFancyStringWithCaller()}");
                }
            }
        }

        public async Task LaunchCheckerTask(CheckerTask task)
        {
            using var scope = this.logger.BeginEnoScope(task);
            try
            {
                this.logger.LogTrace($"LaunchCheckerTask() for task {task.Id} ({task.Method}, currentRound={task.CurrentRoundId}, relatedRound={task.RelatedRoundId})");
                var cancelSource = new CancellationTokenSource();
                var now = DateTime.UtcNow;
                var span = task.StartTime.Subtract(DateTime.UtcNow);
                if (span.TotalSeconds < -0.5)
                {
                    this.logger.LogWarning($"Task {task.Id} starts {span.TotalSeconds} late (should: {task.StartTime})");
                }

                if (task.StartTime > now)
                {
                    this.logger.LogTrace($"Task {task.Id} sleeping: {span}");
                    await Task.Delay(span);
                }

                var content = new StringContent(
                    JsonSerializer.Serialize(
                        new CheckerTaskMessage(
                            task.Id,
                            task.Method,
                            task.Address,
                            task.TeamId,
                            task.TeamName,
                            task.CurrentRoundId,
                            task.RelatedRoundId,
                            task.Payload,
                            task.VariantId,
                            task.MaxRunningTime,
                            task.RoundLength,
                            task.GetTaskChainId()),
                        EnoCoreUtil.CamelCaseEnumConverterOptions),
                    Encoding.UTF8,
                    "application/json");
                cancelSource.CancelAfter(task.MaxRunningTime);
                this.statistics.LogCheckerTaskLaunchMessage(task);
                this.logger.LogDebug($"LaunchCheckerTask {task.Id} POSTing {task.Method} to checker");
                var response = await Client.PostAsync(task.CheckerUrl, content, cancelSource.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var responseString = (await response.Content.ReadAsStringAsync()).TrimEnd(Environment.NewLine.ToCharArray());
                    this.logger.LogDebug($"LaunchCheckerTask received {responseString}");
                    var resultMessage = JsonSerializer.Deserialize<CheckerResultMessage>(responseString, EnoCoreUtil.CamelCaseEnumConverterOptions);
                    var checkerResult = resultMessage!.Result;
                    this.logger.LogDebug($"LaunchCheckerTask {task.Id} returned {checkerResult} with Message {resultMessage.Message}");
                    var errorMessage = resultMessage.Message?.Replace("\0", string.Empty); // pgsql does NOT like 0 chars in utf8 strings
                    var attackInfo = resultMessage.AttackInfo?.Replace("\0", string.Empty);

                    if (resultMessage.Message != errorMessage)
                    {
                        this.logger.LogWarning("LaunchCheckerTask had message with 0 char in message");
                    }

                    if (resultMessage.AttackInfo != attackInfo)
                    {
                        this.logger.LogWarning("LaunchCheckerTask had attackInfo with 0 char in message");
                    }

                    CheckerTask updatedTask = task with {
                        CheckerResult = checkerResult,
                        ErrorMessage = errorMessage,
                        AttackInfo = attackInfo,
                        CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done
                    };
                    this.statistics.LogCheckerTaskFinishedMessage(updatedTask);
                    ResultsQueue.Enqueue(updatedTask);
                    return;
                }
                else
                {
                    this.logger.LogError($"LaunchCheckerTask {task.Id} {task.Method} returned {response.StatusCode} ({(int)response.StatusCode})");
                    var updatedTask = task with { CheckerResult = CheckerResult.INTERNAL_ERROR, CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done };
                    this.statistics.LogCheckerTaskFinishedMessage(updatedTask);
                    ResultsQueue.Enqueue(updatedTask);
                    return;
                }
            }
            catch (TaskCanceledException)
            {
                this.logger.LogError($"{nameof(this.LaunchCheckerTask)} {task.Id} {task.Method}  was cancelled because it did not finish");
                var updatedTask = task with { CheckerResult = CheckerResult.OFFLINE, CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done };
                this.statistics.LogCheckerTaskFinishedMessage(updatedTask);
                ResultsQueue.Enqueue(updatedTask);
            }
            catch (Exception e)
            {
                this.logger.LogError($"{nameof(this.LaunchCheckerTask)} {task.Id} failed: {e.ToFancyStringWithCaller()}");
                var updatedTask = task with { CheckerResult = CheckerResult.INTERNAL_ERROR, CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done };
                this.statistics.LogCheckerTaskFinishedMessage(updatedTask);
                ResultsQueue.Enqueue(updatedTask);
            }
        }

        internal static void Main()
        {
            const string mutexId = @"Global\EnoLauncher";
            using var mutex = new Mutex(false, mutexId, out var _);
            try
            {
                if (mutex.WaitOne(10, false))
                {
                    var serviceProvider = new ServiceCollection()
                        .AddScoped<IEnoDatabase, EnoDatabase>()
                        .AddDbContextPool<EnoDatabaseContext>(
                            options =>
                            {
                                options.UseNpgsql(
                                    EnoDatabaseContext.PostgresConnectionString,
                                    pgoptions => pgoptions.EnableRetryOnFailure());
                            },
                            90)
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

        internal async Task UpdateDatabaseLoop()
        {
            try
            {
                while (!LauncherCancelSource.IsCancellationRequested)
                {
                    CheckerTask[]? results = new CheckerTask[TaskUpdateBatchSize];
                    int i = 0;
                    for (; i < TaskUpdateBatchSize; i++)
                    {
                        if (ResultsQueue.TryDequeue(out var result))
                        {
                            results[i] = result;
                        }
                        else
                        {
                            break;
                        }
                    }

                    try
                    {
                        using (var scope = this.serviceProvider.CreateScope())
                        {
                            var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                            await db.UpdateTaskCheckerTaskResults(results.AsMemory(0, i));
                        }

                        if (i != TaskUpdateBatchSize)
                        {
                            await Task.Delay(1);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        this.logger.LogInformation($"UpdateDatabase dropping update because: {e.ToFancyStringWithCaller()}");
                        if (results != null)
                        {
                            foreach (var task in results)
                            {
                                this.logger.LogCritical(task.ToString());
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception e)
            {
                this.logger.LogCritical($"UpdateDatabase failed : {e.ToFancyStringWithCaller()}");
            }
        }
    }
}
