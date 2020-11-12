namespace EnoCore.JsonConfiguration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class JsonConfigurationValidationException : Exception
    {
        public JsonConfigurationValidationException(string message)
            : base(message)
        {
        }

        public JsonConfigurationValidationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
