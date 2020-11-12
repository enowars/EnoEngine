namespace EnoCore.JsonConfiguration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class JsonConfigurationServiceValidationException : JsonConfigurationValidationException
    {
        public JsonConfigurationServiceValidationException(string message)
            : base(message)
        {
        }

        public JsonConfigurationServiceValidationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
