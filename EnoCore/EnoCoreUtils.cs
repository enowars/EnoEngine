using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EnoCore
{

    ///
    /// code used from https://devblogs.microsoft.com/pfxteam/getting-random-numbers-in-a-thread-safe-way/
    public static class ThreadSafeRandom
    {
        private static readonly RNGCryptoServiceProvider _global = new RNGCryptoServiceProvider();
        [ThreadStatic]
        private static Random _local;

        public static int Next()
        {
            Random inst = _local;
            if (inst == null)
            {
                byte[] buffer = new byte[4];
                _global.GetBytes(buffer);
                _local = inst = new Random(
                    BitConverter.ToInt32(buffer, 0));
            }
            return inst.Next();
        }

        public static int Next(int n) {
            return Next() % n;
        }
    }

    public class EnoCoreUtils
    {
        private static readonly RNGCryptoServiceProvider Random = new RNGCryptoServiceProvider();
        private static readonly int ENTROPY_IN_BYTES = 8;
        private static readonly byte[] FLAG_SIGNING_KEY = Encoding.ASCII.GetBytes("suchasecretstornkkeytheywillneverguess");
        private static readonly byte[] NOISE_SIGNING_KEY = Encoding.ASCII.GetBytes("anotherstrenksecrettheyvref24tr");
        public static string PostgresDomain => Environment.GetEnvironmentVariable("DATABASE_DOMAIN") ?? "localhost";
        public static string PostgresConnectionString => $@"Server={PostgresDomain};Port=5432;Database=EnoDatabase;User Id=docker;Password=docker;Timeout=15;SslMode=Disable;";


        public static CheckerResult ParseCheckerResult(string result)
        {
            switch (result) {
                case "INTERNAL_ERROR":
                    return CheckerResult.CheckerError;
                case "OK":
                    return CheckerResult.Ok;
                case "MUMBLE":
                    return CheckerResult.Mumble;
                case "OFFLINE":
                    return CheckerResult.Down;
                default:
                    return CheckerResult.CheckerError;
            }
        }

        public static string FormatException(Exception e)
        {
            string fancy = $"{e.Message} ({e.GetType()})\n{e.StackTrace}";
            if (e.InnerException != null)
            {
                fancy += $"\nInnerException:\n{FormatException(e.InnerException)}";
            }
            return fancy;
        }

        public static void GenerateCurrentScoreboard(EnoEngineScoreboard scoreboard, string path, long roundId)
        {
            var json = JsonConvert.SerializeObject(scoreboard);
            File.WriteAllText($"{path}scoreboard{roundId}.json", json);
            File.WriteAllText($"{path}scoreboard.json", json);
        }

        internal static string GenerateSignedFlag(int roundId, int teamid)
        {
            using (HMACSHA1 hmacsha1 = new HMACSHA1(FLAG_SIGNING_KEY))
            {
                return GeneratedSignedString(hmacsha1, roundId, teamid);
            }
        }

        internal static string GenerateSignedNoise(int roundId, int teamId)
        {
            using (HMACSHA1 hmacsha1 = new HMACSHA1(NOISE_SIGNING_KEY))
            {
                return GeneratedSignedString(hmacsha1, roundId, teamId);
            }
        }

        public static bool IsValidFlag(string input)
        {
            try
            {
                var flag = input.Substring(3);
                var flagBytes = Convert.FromBase64String(flag);
                var flagContent = new ArraySegment<byte>(flagBytes, 0, sizeof(int) + ENTROPY_IN_BYTES);
                var flagSignature = new ArraySegment<byte>(flagBytes, sizeof(int) + ENTROPY_IN_BYTES,
                                                           flagBytes.Length - sizeof(int) - ENTROPY_IN_BYTES);
                using (HMACSHA1 hmacsha1 = new HMACSHA1(FLAG_SIGNING_KEY))
                {
                    byte[] hash = hmacsha1.ComputeHash(flagBytes, 0, sizeof(int) + ENTROPY_IN_BYTES);
                    return flagSignature.SequenceEqual(hash);
                }
            }
            catch (Exception) { }
            return false;
        }

        private static string GeneratedSignedString(HMAC hmac, int roundId, int teamId)
        {
            byte[] flagContent = new byte[sizeof(int) + ENTROPY_IN_BYTES];
            Random.GetBytes(flagContent, sizeof(int), ENTROPY_IN_BYTES);
            BitConverter.GetBytes(roundId).CopyTo(flagContent, 0);
            byte[] flagSignature = hmac.ComputeHash(flagContent);
            byte[] flag = new byte[flagContent.Length + flagSignature.Length];
            flagContent.CopyTo(flag, 0);
            flagSignature.CopyTo(flag, flagContent.Length);
            return "ENO" + Convert.ToBase64String(flag);
        }

        public static async Task DelayUntil(DateTime time, CancellationToken token)
        {
            var now = DateTime.UtcNow;
            if (now > time)
            {
                return;
            }
            var diff = time - now;
            await Task.Delay(diff, token);
        }

        internal static string ExtractSubnet(string subnetIP, int subnetBytesLength)
        {
            var ip = IPAddress.Parse(subnetIP);
            byte[] teamSubnet = new byte[subnetBytesLength];
            Array.Copy(ip.GetAddressBytes(), teamSubnet, subnetBytesLength);
            return BitConverter.ToString(teamSubnet);
        }

        ///
        /// code taken from https://stackoverflow.com/questions/1287567/is-using-random-and-orderby-a-good-shuffle-algorithm/1287572#1287572
        public static IEnumerable<T> Shuffle<T>(IEnumerable<T> source)
        {
            T[] elements = source.ToArray();
            for (int i = elements.Length - 1; i >= 0; i--)
            {
                // Swap element "i" with a random earlier element it (or itself)
                // ... except we don't really need to swap it fully, as we can
                // return it immediately, and afterwards it's irrelevant.
                int swapIndex = ThreadSafeRandom.Next(i + 1);
                yield return elements[swapIndex];
                elements[swapIndex] = elements[i];
            }
        }

        public static ServiceStatus CheckerResultToServiceStatus(CheckerResult checkerResult)
        {
            switch (checkerResult)
            {
                case CheckerResult.Ok:
                    return ServiceStatus.Ok;
                case CheckerResult.Mumble:
                    return ServiceStatus.Mumble;
                case CheckerResult.Down:
                    return ServiceStatus.Down;
                default:
                    return ServiceStatus.CheckerError;
            }
        }
    }
}
