using EnoCore;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
                foreach (var task in tasks)
                {
                    var t = Task.Run(() =>
                    {
                        var httpClient = new HttpClient();
                        var request = new CheckerTaskRequest();
                        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                        Logger.LogTrace($"Requesting CheckerTask {task.Id}");
                        httpClient.PostAsync(new Uri(servicesDict[task.ServiceName].Checkers[0]), content);
                    });
                }
                await Task.Delay(500, LauncherCancelSource.Token);
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
