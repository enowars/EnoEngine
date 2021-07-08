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
    using Json.Schema;
    using Json.Schema.Generation;

    public class EnoCoreUtil
    {
        public const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        private static bool directoryCreated = false;

        public static string DataDirectory
        {
            get
            {
                var path = $"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}";

                // Auto create the directory if it does not exist
                if (!directoryCreated)
                {
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    directoryCreated = true;
                }

                return path;
            }
        }

        public static JsonSchema GenerateSchema()
        {
            var schemaBuilder = new JsonSchemaBuilder();
            var options = new SchemaGeneratorConfiguration
            {
                PropertyNamingMethod = PropertyNamingMethods.CamelCase,
            };
            var schema = schemaBuilder.FromType<JsonConfiguration>(options).Build();
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
