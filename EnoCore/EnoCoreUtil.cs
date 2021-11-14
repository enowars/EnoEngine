namespace EnoCore;
public class EnoCoreUtil
{
    public const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    public static readonly string DataDirectory = $"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}";
    public static readonly JsonSerializerOptions CamelCaseEnumConverterOptions = new JsonSerializerOptions
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string GetCurrentTimestamp()
    {
        return DateTime.UtcNow.ToString(DateTimeFormat);
    }

    public static double SecondsSinceEpoch(DateTime dt)
    {
        return dt.Subtract(DateTime.UnixEpoch).TotalSeconds;
    }
}
