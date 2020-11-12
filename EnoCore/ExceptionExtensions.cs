namespace EnoCore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

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
}
