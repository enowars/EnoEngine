namespace EnoCore.Models.JsonConfiguration;

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
