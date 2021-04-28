namespace EnoChecker.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Checker;
    using EnoCore.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    [ApiController]
    [Route("/")]
    [Route("/service")]
    public class CheckerController : Controller
    {
        private readonly ILogger logger;
        private readonly IChecker checker;
        private readonly ICheckerInitializer checkerInitializer;

        public CheckerController(ILogger<CheckerController> logger, IChecker checker, ICheckerInitializer checkerInitializer)
        {
            this.logger = logger;
            this.checker = checker;
            this.checkerInitializer = checkerInitializer;
        }

        [HttpPost]
        [Route("/")]
        public async Task<IActionResult> Flag([FromBody] CheckerTaskMessage ctm)
        {
            // TODO merge .RequestAborted with timer
            using var scope = this.logger.BeginEnoScope(ctm);
            this.logger.LogDebug(ctm.ToString());
            try
            {
                if (ctm.Method == CheckerTaskMethod.putflag)
                {
                    await this.checker.HandlePutFlag(ctm, this.HttpContext.RequestAborted);
                }
                else if (ctm.Method == CheckerTaskMethod.getflag)
                {
                    await this.checker.HandleGetFlag(ctm, this.HttpContext.RequestAborted);
                }
                else if (ctm.Method == CheckerTaskMethod.putnoise)
                {
                    await this.checker.HandlePutNoise(ctm, this.HttpContext.RequestAborted);
                }
                else if (ctm.Method == CheckerTaskMethod.getnoise)
                {
                    await this.checker.HandleGetNoise(ctm, this.HttpContext.RequestAborted);
                }
                else if (ctm.Method == CheckerTaskMethod.havoc)
                {
                    await this.checker.HandleHavoc(ctm, this.HttpContext.RequestAborted);
                }
                else
                {
                    throw new Exception("invalid method");
                }

                this.logger.LogInformation($"Task {ctm.TaskId} succeeded");
                return this.Json(new CheckerResultMessage(CheckerResult.OK, null));
            }
            catch (OperationCanceledException)
            {
                this.logger.LogWarning($"Task {ctm.TaskId} was cancelled");
                return this.Json(new CheckerResultMessage(CheckerResult.OFFLINE, null));
            }
            catch (MumbleException e)
            {
                this.logger.LogWarning($"Task {ctm.TaskId} has failed: {e.ToFancyString()}");
                return this.Json(new CheckerResultMessage(CheckerResult.MUMBLE, e.Message));
            }
            catch (OfflineException e)
            {
                this.logger.LogWarning($"Task {ctm.TaskId} has failed: {e.ToFancyString()}");
                return this.Json(new CheckerResultMessage(CheckerResult.OFFLINE, e.Message));
            }
            catch (Exception e)
            {
                this.logger.LogError($"Task {ctm.TaskId} has failed: {e.ToFancyString()}");
                return this.Json(new CheckerResultMessage(CheckerResult.INTERNAL_ERROR, null));
            }
        }

        [HttpGet]
        [Route("/service")]
        public IActionResult Service()
        {
            var cim = new CheckerInfoMessage(this.checkerInitializer.ServiceName, this.checkerInitializer.FlagVariants, this.checkerInitializer.NoiseVariants, this.checkerInitializer.HavocVariants);
            return this.Json(cim, null);
        }

        [HttpGet]
        [Route("/")]
        public IActionResult Index()
        {
            return this.File("./post.html", "text/html");
        }
    }
}
