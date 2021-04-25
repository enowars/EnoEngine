namespace EnoCore
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using EnoCore.Configuration;
    using Newtonsoft.Json.Schema;
    using Newtonsoft.Json.Schema.Generation;
    using Newtonsoft.Json.Serialization;

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

        public static JSchema GenerateSchema()
        {
            JSchemaGenerator generator = new JSchemaGenerator();
            generator.SchemaIdGenerationHandling = SchemaIdGenerationHandling.TypeName;
            generator.GenerationProviders.Add(new StringEnumGenerationProvider());
            generator.ContractResolver = new CamelCasePropertyNamesContractResolver();
            generator.SchemaReferenceHandling = SchemaReferenceHandling.None;
            generator.DefaultRequired = Newtonsoft.Json.Required.Default;
            JSchema schema = generator.Generate(typeof(JsonConfiguration));
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
