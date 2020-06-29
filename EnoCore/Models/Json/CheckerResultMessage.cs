using EnoCore.Models.Database;
using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public class CheckerResultMessage
    {
        [JsonPropertyName("result")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CheckerResult Result { get; set; } = CheckerResult.INTERNAL_ERROR;
        [JsonPropertyName("message")]
        public string Message { get; set; } = default!;
    }
    public class CheckerInfoMessage
    {
        [JsonPropertyName("serviceName")]
        public string ServiceName { get; set; } = default!;
        [JsonPropertyName("flagCount")]
        public long FlagCount { get; set; }
        [JsonPropertyName("noiseCount")]
        public long NoiseCount { get; set; }
        [JsonPropertyName("havocCount")]
        public long HavocCount { get; set; }

    }
}
