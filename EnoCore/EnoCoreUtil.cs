namespace EnoCore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class EnoCoreUtil
    {
        public const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        public static string GetCurrentTimestamp()
        {
            return DateTime.UtcNow.ToString(DateTimeFormat);
        }
    }
}
