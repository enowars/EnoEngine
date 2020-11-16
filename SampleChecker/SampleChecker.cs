namespace SampleChecker
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Checker;
    using EnoCore.Models;
    using Microsoft.Extensions.Logging;

    public class SampleChecker : IChecker
    {
        private readonly ILogger<SampleChecker> logger;

        public SampleChecker(ILogger<SampleChecker> logger, SampleSingleton sampleSingleton)
        {
            this.logger = logger;
            logger.LogInformation($"SampleChecker {sampleSingleton}");
        }

        public Task HandleGetFlag(CheckerTaskMessage task, CancellationToken token)
        {
            this.logger.LogDebug($"{nameof(this.HandleGetFlag)}");
            return Task.CompletedTask;
        }

        public Task HandleGetNoise(CheckerTaskMessage task, CancellationToken token)
        {
            this.logger.LogDebug($"{nameof(this.HandleGetNoise)}");
            return Task.CompletedTask;
        }

        public Task HandleHavoc(CheckerTaskMessage task, CancellationToken token)
        {
            this.logger.LogDebug($"{nameof(this.HandleHavoc)}");
            return Task.CompletedTask;
        }

        public Task HandlePutFlag(CheckerTaskMessage task, CancellationToken token)
        {
            this.logger.LogDebug($"{nameof(this.HandlePutFlag)}");
            return Task.CompletedTask;
        }

        public Task HandlePutNoise(CheckerTaskMessage task, CancellationToken token)
        {
            this.logger.LogDebug($"{nameof(this.HandlePutNoise)}");
            return Task.CompletedTask;
        }
    }
}
