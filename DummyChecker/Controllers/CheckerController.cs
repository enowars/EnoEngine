using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using EnoCore;
using EnoCore.Models;

namespace DummyChecker.Controllers
{
    /// <summary>
    /// Dummy checker for EnoEngine tests.
    /// </summary>
    [ApiController]
    [Route("/")]
    [Route("/service")]
    public class CheckerController : Controller
    {
        private readonly ILogger Logger;

        /// <summary>
        /// Create a CheckerController with an appropriate ILogger
        /// </summary>
        /// <param name="logger"></param>
        public CheckerController(ILogger<CheckerController> logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Flag endpoint
        /// </summary>
        /// <param name="ctm">CheckerTaskMessage for the task</param>
        /// <returns></returns>
        [HttpPost]
        [Route("/")]
        public IActionResult Flag([FromBody] CheckerTaskMessage ctm)
        {
            Logger.LogDebug(ctm.ToString());
            return Ok("{ \"result\": \"OK\" }");
        }

        /// <summary>
        /// Service endpoint
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("/service")]
        public IActionResult Service()
        {
            var cim = new CheckerInfoMessage("DummyChecker", 1, 1, 1);
            return Ok(JsonSerializer.Serialize(cim, EnoCoreUtil.CamelCaseEnumConverterOptions));
        }
    }
}
