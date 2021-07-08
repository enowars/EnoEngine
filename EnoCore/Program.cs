namespace Application
{
    using System.IO;
    using System.Text.Json;
    using EnoCore;
    using EnoCore.Models.JsonConfiguration;
    using Json.More;
    using Json.Schema.Generation;
    using NJsonSchema;

    internal class Program
    {
        private static void Main(string[] args)
        {
            File.WriteAllText("ctf.schema.json", JsonSerializer.Serialize(EnoCoreUtil.GenerateSchema()));
        }
    }
}
