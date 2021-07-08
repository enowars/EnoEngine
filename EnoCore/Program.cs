namespace Application
{
    using System.IO;
    using System.Text.Json;
    using EnoCore;

    internal class Program
    {
        private static void Main(string[] args)
        {
            File.WriteAllText("ctf.schema.json", JsonSerializer.Serialize(EnoCoreUtil.GenerateSchema()));
        }
    }
}
