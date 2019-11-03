using EnoCore.Models;
using EnoCore.Models.Database;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore
{
    public static class LoggerExtensions
    {
        public static IDisposable BeginEnoScope(this ILogger logger, CheckerTask checkerTask)
        {
            return logger.BeginScope(new Dictionary<string, object> {
                    [nameof(CheckerTask)] = checkerTask
            });
        }

        public static IDisposable BeginEnoScope(this ILogger logger, long roundId)
        {
            return logger.BeginScope(new Dictionary<string, object> {
                {
                    "round", roundId
                }
            });
        }
    }

}
