namespace EnoCore.Checker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class OfflineException : Exception
    {
        public OfflineException(string scoreboardMessage)
        {
            this.ScoreboardMessage = scoreboardMessage;
        }

        public string ScoreboardMessage { get; set; }
    }
}
