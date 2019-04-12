using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnoCore;
using EnoCore.Models.Database;
using Microsoft.AspNetCore.Mvc;

namespace EnoLogs.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InsertLogsController : ControllerBase
    {
        static ConcurrentQueue<CheckerLogMessage> LogQueue = new ConcurrentQueue<CheckerLogMessage>();
        [HttpPost]
        public async Task Post([FromBody] CheckerLogMessage value)
        {
            if (LogQueue.Count() < 10000)
            {
                LogQueue.Enqueue(value);
            }
            if (LogQueue.Count() > 1000)
            {
                await EnoDatabase.InsertCheckerLogMessages(LogQueue.Take(1000));
            }
        }
    }
}
