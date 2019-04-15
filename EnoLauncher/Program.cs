using EnoCore;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
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
        private static readonly CancellationTokenSource LauncherCancelSource = new CancellationTokenSource();
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoLauncher));
        private static readonly HttpClient Client = new HttpClient();
        private readonly Dictionary<string, JsonConfigurationService> ServicesDict;
        public static JsonConfiguration Configuration { get; set; }

        public Program(Dictionary<string, JsonConfigurationService> servicesDict)
        {
            ServicesDict = servicesDict;
        }

        public void Start()
        {
            Client.Timeout = new TimeSpan(0, 1, 0);
            LauncherLoop().Wait();
        }

        public async Task LauncherLoop()
        {
            while (!LauncherCancelSource.IsCancellationRequested)
            {
                var tasks = await EnoDatabase.RetrievePendingCheckerTasks(100);
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
                    var t = LaunchCheckerTask(task);
                }
                if (tasks.Count == 0)
                {
                    await Task.Delay(50, LauncherCancelSource.Token);
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
                message.Message = $"LaunchCheckerTask for task {task.Id}";
                Logger.LogTrace(message);
                var cancelSource = new CancellationTokenSource();
                var now = DateTime.Now;
                var span = task.StartTime.Subtract(DateTime.Now);
                if (span.TotalSeconds < -0.5)
                {
                    message.Message = $"Task starts {span.TotalSeconds} late";
                    Logger.LogWarning(message);
                }
                if (task.StartTime > now)
                {
                    await Task.Delay(span);
                }
                cancelSource.CancelAfter(task.MaxRunningTime * 1000);
                var content = new StringContent(JsonConvert.SerializeObject(task), Encoding.UTF8, "application/json");
                message.Message = $"LaunchCheckerTask {task.Id} POSTing to checker";
                Logger.LogTrace(message);
                var response = await Client.PostAsync(new Uri(ServicesDict[task.ServiceName].Checkers[0] + "/"), content, cancelSource.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    dynamic responseJson = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                    string result = responseJson.result;
                    var checkerResult = EnoCoreUtils.ParseCheckerResult(result);
                    message.Message = $"LaunchCheckerTask {task.Id} returned {checkerResult}";
                    Logger.LogTrace(message);
                    await EnoDatabase.UpdateTaskCheckerTaskResult(task.Id, checkerResult);
                }
                else
                {
                    message.Message = $"LaunchCheckerTask {task.Id} returned error code {response.StatusCode}";
                    Logger.LogError(message);
                    await EnoDatabase.UpdateTaskCheckerTaskResult(task.Id, CheckerResult.CheckerError);
                }
            }
            catch (Exception e)
            {
                var message = EnoLogMessage.FromCheckerTask(task);
                message.Module = nameof(EnoLauncher);
                message.Function = nameof(LaunchCheckerTask);
                message.Message = $"{nameof(LaunchCheckerTask)} failed: {EnoCoreUtils.FormatException(e)}";
                Logger.LogError(message);
                await EnoDatabase.UpdateTaskCheckerTaskResult(task.Id, CheckerResult.CheckerError);
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
                EnoDatabase.Migrate(Logger);
                var content = File.ReadAllText("ctf.json");
                Configuration = JsonConvert.DeserializeObject<JsonConfiguration>(content);
                var servicesDict = new Dictionary<string, JsonConfigurationService>();
                foreach (var service in Configuration.Services)
                {
                    servicesDict.Add(service.Name, service);
                }
                new Program(servicesDict).Start();
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
    }
}
