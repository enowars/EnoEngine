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
    //using NJsonSchema;
    using NJsonSchema.Generation.TypeMappers;

    public class EnoCoreUtil
    {
        public const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        public static readonly string DataDirectory = $"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}";
        public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        public static JsonSchema GenerateSchema()
        {
            //var schema = JsonSchema.FromType<JsonConfiguration>(
            //    new NJsonSchema.Generation.JsonSchemaGeneratorSettings
            //    {
            //        SerializerSettings = {
            //        },
            //        TypeMappers =
            //        {
            //            new PrimitiveTypeMapper(typeof(IPAddress), s => s.Type = JsonObjectType.String),
            //        },
            //    });

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
