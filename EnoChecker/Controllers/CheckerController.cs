namespace GamemasterChecker.Controllers
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

    /// <summary>
    /// Dummy checker for EnoEngine tests.
    /// </summary>
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
        public IActionResult Flag([FromBody] CheckerTaskMessage ctm)
        {
            // TODO merge .RequestAborted with timer
            using var scope = this.logger.BeginEnoScope(ctm);
            this.logger.LogDebug(ctm.ToString());
            try
            {
                if (ctm.Method == CheckerTaskMethod.putflag)
                {
                    this.checker.HandlePutFlag(ctm, this.HttpContext.RequestAborted);
                }
                else if (ctm.Method == CheckerTaskMethod.getflag)
                {
                    this.checker.HandleGetFlag(ctm, this.HttpContext.RequestAborted);
                }
                else if (ctm.Method == CheckerTaskMethod.putnoise)
                {
                    this.checker.HandlePutNoise(ctm, this.HttpContext.RequestAborted);
                }
                else if (ctm.Method == CheckerTaskMethod.getnoise)
                {
                    this.checker.HandleGetNoise(ctm, this.HttpContext.RequestAborted);
                }
                else if (ctm.Method == CheckerTaskMethod.havoc)
                {
                    this.checker.HandleHavoc(ctm, this.HttpContext.RequestAborted);
                }
                else
                {
                    throw new Exception();
                }

                return this.Json(new CheckerResultMessage(CheckerResult.OK, null));
            }
            catch (OperationCanceledException)
            {
                return this.Json(new CheckerResultMessage(CheckerResult.OFFLINE, null));
            }
            catch (MumbleException e)
            {
                return this.Json(new CheckerResultMessage(CheckerResult.MUMBLE, e.Message));
            }
            catch (OfflineException e)
            {
                return this.Json(new CheckerResultMessage(CheckerResult.OFFLINE, e.Message));
            }
            catch
            {
                return this.Json(new CheckerResultMessage(CheckerResult.INTERNAL_ERROR, null));
            }
        }

        [HttpGet]
        [Route("/service")]
        public IActionResult Service()
        {
            var cim = new CheckerInfoMessage(this.checkerInitializer.ServiceName, 1, 1, 1);
            return this.Json(cim, null);
        }
    }
}
