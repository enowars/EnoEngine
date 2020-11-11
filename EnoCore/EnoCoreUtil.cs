using EnoCore.Models.Database;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace EnoCore
{
    public static class ExceptionExtensions
    {
        public static string ToFancyString(this Exception e, [CallerMemberName] string memberName = "", bool full = true)
        {
            string fancy = $"{memberName} failed: {e.Message} ({e.GetType()})\n{e.StackTrace}";
            if (e.InnerException != null)
            {
                fancy += $"\nInnerException:\n{e.InnerException.ToFancyString(full)}";
            }
            return fancy;
        }
        private static string ToFancyString(this Exception e, bool full = true)
        {
            string fancy = $"{e.Message} ({e.GetType()})\n{e.StackTrace}";
            if (e.InnerException != null)
            {
                fancy += $"\nInnerException:\n{e.InnerException.ToFancyString(full)}";
            }
            return fancy;
        }
    }

    public static class LoggerExtensions
    {
        public static IDisposable BeginEnoScope(this ILogger logger, CheckerTask checkerTask)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
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

    public static class CheckerResultExtensions
    {
        public static ServiceStatus AsServiceStatus(this CheckerResult checkerResult)
        {
            return checkerResult switch
            {
                CheckerResult.OK => ServiceStatus.OK,
                CheckerResult.MUMBLE => ServiceStatus.MUMBLE,
                CheckerResult.OFFLINE => ServiceStatus.OFFLINE,
                _ => ServiceStatus.INTERNAL_ERROR,
            };
        }
    }
}
