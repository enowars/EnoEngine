using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace EnoCore.Logging
{
    public static class ExceptionExtensions
    {
        public static string ToFancyString(this Exception e, [CallerMemberName] string memberName = "", bool full = true)
        {
            string fancy = $"{memberName} failed: {e.Message} ({e.GetType()})\n{e.StackTrace}";
            if (e.InnerException != null)
            {
                fancy += $"\nInnerException:\n{e.InnerException.ToFancyString(memberName, full)}";
            }
            return fancy;
        }
    }
}
