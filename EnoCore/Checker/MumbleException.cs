namespace EnoCore.Checker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class MumbleException : Exception
    {
        public MumbleException(string scoreboardMessage)
            : base(scoreboardMessage)
        {
        }
    }
}
