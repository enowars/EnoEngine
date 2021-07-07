namespace EnoCore
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using EnoCore.Configuration;
    using EnoCore.Models.JsonConfiguration;
    using NJsonSchema;
    using NJsonSchema.Generation.TypeMappers;

    public class EnoCoreUtil
    {
        public const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        public static readonly string DataDirectory = $"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}";
        public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        public static JsonSchema GenerateSchema()
        {
            var schema = JsonSchema.FromType<JsonConfiguration>(
                new NJsonSchema.Generation.JsonSchemaGeneratorSettings
                {
                    TypeMappers = {
                        new PrimitiveTypeMapper(typeof(IPAddress), s => s.Type = JsonObjectType.String),
                    },
                });
            return schema;
        }

        public static string GetCurrentTimestamp()
        {
            return DateTime.UtcNow.ToString(DateTimeFormat);
        }

        public static double SecondsSinceEpoch(DateTime dt)
        {
            return dt.Subtract(DateTime.UnixEpoch).TotalSeconds;
        }
    }
}
