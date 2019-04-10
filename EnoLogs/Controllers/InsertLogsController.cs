using System;
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
        [HttpPost]
        public async Task Post([FromBody] CheckerLogMessage value)
        {
            await EnoDatabase.InsertCheckerLogMessage(value);
        }
    }
}
