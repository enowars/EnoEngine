using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public class CheckerResultMessage
    {
        [JsonPropertyName("result")]
        public string Result { get; set; } = default!;
        public string Message { get; set; } = default!;
    }
}
