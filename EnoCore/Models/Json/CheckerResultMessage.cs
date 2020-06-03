using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace EnoCore.Models.Json
{
    public class CheckerResultMessage
    {
        [JsonPropertyName("result")]
        public string Result { get; set; } = default!;
    }
}
