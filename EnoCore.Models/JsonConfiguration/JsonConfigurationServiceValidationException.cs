namespace EnoCore.Models.JsonConfiguration;

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
