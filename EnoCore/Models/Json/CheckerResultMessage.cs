using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public class CheckerResultMessage
    {
        [JsonPropertyName("result")]
        public string Result { get; set; } = default!;
        [JsonPropertyName("Message")]
        public string Message { get; set; } = default!;
    }
}
