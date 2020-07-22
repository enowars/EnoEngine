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
        public IActionResult Flag([FromBody] CheckerTaskMessage _)
        {
            return Ok("{ \"result\": \"OK\" }");
        }
        [HttpGet]
        [Route("/service")]
        public IActionResult Service()
        {
            return Ok(JsonSerializer.Serialize(new CheckerInfoMessage
            {
                ServiceName = "DummyChecker",
                FlagCount = 1,
                NoiseCount = 1,
                HavocCount = 1
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
            ));
        }
    }
}
