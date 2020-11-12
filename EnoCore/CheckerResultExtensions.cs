namespace EnoCore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using EnoCore.Models;

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
