using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using EnoCore.Models.Json;

namespace GamemasterChecker.Controllers
{
    [ApiController]
    [Route("/")]
    [Route("/service")]
    public class CheckerController : Controller
    {
        [HttpPost]
        [Route("/")]
        public IActionResult Flag([FromBody] CheckerTaskMessage content)
        {
            return Ok("{ \"result\": \"OK\" }");
        }
    }
}
