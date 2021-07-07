namespace Application
{
    using System.IO;
    using EnoCore;
    using EnoCore.Models.JsonConfiguration;
    using NJsonSchema;

    internal class Program
    {
        private static void Main(string[] args)
        {
            File.WriteAllText("ctf.schema.json", EnoCoreUtil.GenerateSchema().ToJson());
        }
    }
}
