namespace EnoCore.JsonConfiguration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class JsonConfigurationTeamValidationException : JsonConfigurationValidationException
    {
        public JsonConfigurationTeamValidationException(string message)
            : base(message)
        {
        }

        public JsonConfigurationTeamValidationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
