namespace Application
{
    using System.IO;
    using EnoCore;
    using EnoCore.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Schema;
    using Newtonsoft.Json.Schema.Generation;
    using Newtonsoft.Json.Serialization;

    internal class Program
    {
        private static void Main(string[] args)
        {
            File.WriteAllText("ctf.schema.json", EnoCoreUtil.GenerateSchema().ToString());
        }
    }
}
