using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EnoCore
{
    public class EnoCoreUtils
    {
        public static readonly LoggerFactory Loggers = new LoggerFactory();
        private static readonly Random Random = new Random();
        private static readonly int IPV4_SUBNET_SIZE = 24;
        private static readonly int IPV6_SUBNET_SIZE = 64;

        public static string FormatException(Exception e)
        {
            string fancy = $"{e.Message} ({e.GetType()})\n{e.StackTrace}";
            if (e.InnerException != null)
            {
                fancy += $"\nInnerException:\n{FormatException(e.InnerException)}";
            }
            return fancy;
        }

        public static void InitLogging()
        {
            EnoCoreUtils.Loggers.AddProvider(new EnoEngineConsoleLoggerProvider());
        }

        public static void GenerateCurrentScoreboard(string path)
        {
            var scoreboard = EnoDatabase.GetCurrentScoreboard();
            var json = JsonConvert.SerializeObject(scoreboard);
            File.WriteAllText(path, json);
        }

        public static bool IsSameSubnet(string ipA, string ipB)
        {
            IPAddress addressA = IPAddress.Parse(ipA);
            IPAddress addressB = IPAddress.Parse(ipB);
            return IsSameSubnet(addressA, addressB);
        }

        public static bool IsSameSubnet(IPAddress ipA, IPAddress ipB)
        {
            var a = new BitArray(ipA.GetAddressBytes());
            var b = new BitArray(ipB.GetAddressBytes());

            if (a.Length != b.Length)
                return false;
            int subnetLength;
            if (a.Length == 32)
                subnetLength = IPV4_SUBNET_SIZE;
            else
                subnetLength = IPV6_SUBNET_SIZE;
            for (int i = 0; i < subnetLength; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        internal static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Next(s.Length)]).ToArray());
        }
    }
}
