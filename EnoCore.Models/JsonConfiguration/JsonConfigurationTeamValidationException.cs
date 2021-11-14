namespace EnoCore.Models.JsonConfiguration;

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
