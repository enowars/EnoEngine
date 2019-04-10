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
        private static readonly ILogger Logger = EnoCoreUtils.Loggers.CreateLogger<Program>();
        private static readonly HttpClient Client = new HttpClient();
        public static JsonConfiguration Configuration { get; set; }

        public void Start(Dictionary<string, JsonConfigurationService> servicesDict)
        {
            LauncherLoop(servicesDict).Wait();
        }

        public async Task LauncherLoop(Dictionary<string, JsonConfigurationService> servicesDict)
        {
            Logger.LogInformation("LauncherLoop()");
            while (!LauncherCancelSource.IsCancellationRequested)
            {
                var tasks = await EnoDatabase.RetrievePendingCheckerTasks(100);
                if (tasks.Count > 0)
                {
                    Logger.LogTrace($"Scheduling {tasks.Count} tasks");
                }
                foreach (var task in tasks)
                {
                    var t = Task.Run(async () =>
                    {
                        try
                        {
                            var cancelSource = new CancellationTokenSource();
                            var now = DateTime.Now;
                            var span = task.StartTime.Subtract(DateTime.Now);
                            if (span.TotalSeconds < -0.5)
                            {
                                Logger.LogWarning($"Task {task.Id} ({task.TaskType}) for team {task.TeamName} starts {span.TotalSeconds} too late");
                            }
                            if (task.StartTime > now)
                            {
                                await Task.Delay(span);
                            }
                            cancelSource.CancelAfter(task.MaxRunningTime * 1000);
                            var content = new StringContent(JsonConvert.SerializeObject(task), Encoding.UTF8, "application/json");
                            var response = await Client.PostAsync(new Uri(servicesDict[task.ServiceName].Checkers[0] + "/"), content, cancelSource.Token);
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                dynamic responseJson = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                                string result = responseJson.result;
                                await EnoDatabase.UpdateTaskCheckerTaskResult(task.Id, EnoCoreUtils.ParseCheckerResult(result));
                            }
                            else
                            {
                                await EnoDatabase.UpdateTaskCheckerTaskResult(task.Id, CheckerResult.CheckerError);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.LogError($"CheckerTask {task.Id} failed: {EnoCoreUtils.FormatException(e)}");
                            await EnoDatabase.UpdateTaskCheckerTaskResult(task.Id, CheckerResult.CheckerError);
                        }
                    });
                }
                await Task.Delay(50, LauncherCancelSource.Token);
            }
        }

        static void Main(string[] args)
        {
            EnoCoreUtils.InitLogging();
            EnoDatabase.Migrate();
            var content = File.ReadAllText("ctf.json");
            Configuration = JsonConvert.DeserializeObject<JsonConfiguration>(content);
            var servicesDict = new Dictionary<string, JsonConfigurationService>();
            foreach (var service in Configuration.Services)
            {
                servicesDict.Add(service.Name, service);
            }
            new Program().Start(servicesDict);
        }
    }
}
