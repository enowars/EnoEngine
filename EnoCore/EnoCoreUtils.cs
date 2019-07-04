using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.Extensions.DependencyInjection;
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

        public static void NextBytes(byte[] array)
        {
            Random inst = _local;
            if (inst == null)
            {
                byte[] buffer = new byte[4];
                _global.GetBytes(buffer);
                _local = inst = new Random(
                    BitConverter.ToInt32(buffer, 0));
            }
            inst.NextBytes(array);
        }

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
        const int DATABASE_RETRIES = 500;
        internal static readonly int ENTROPY_IN_BYTES = 8;
        internal static readonly byte[] FLAG_SIGNING_KEY = Encoding.ASCII.GetBytes("suchasecretstornkkeytheywillneverguess");
        internal static readonly byte[] NOISE_SIGNING_KEY = Encoding.ASCII.GetBytes("anotherstrenksecrettheyvref24tr");
        public static string PostgresDomain => Environment.GetEnvironmentVariable("DATABASE_DOMAIN") ?? "localhost";
        public static string PostgresConnectionString => $@"Server={PostgresDomain};Port=5432;Database=EnoDatabase;User Id=docker;Password=docker;Timeout=15;SslMode=Disable;";

        public static async Task RetryDatabaseAction(Func<Task> function)
        {
            Exception lastException = null;
            for (int i = 0; i < DATABASE_RETRIES; i++)
            {
                try
                {
                    await function();
                    return;
                }
                catch (SocketException e)
                {
                    lastException = e;
                }
                catch (IOException e)
                {
                    lastException = e;
                }
            }
            throw lastException;
        }

        public static async Task<T> RetryDatabaseAction<T>(Func<Task<T>> function)
        {
            Exception lastException = null;
            for (int i = 0; i < DATABASE_RETRIES; i++)
            {
                try
                {
                    return await function();
                }
                catch (SocketException e)
                {
                    lastException = e;
                }
                catch (IOException e)
                {
                    lastException = e;
                }
            }
            throw lastException;
        }

        public static async Task RetryScopedDatabaseAction(IServiceProvider serviceProvider, Func<IEnoDatabase, Task> function)
        {
            Exception lastException = null;
            for (int i = 0; i < DATABASE_RETRIES; i++)
            {
                try
                {
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                        await function(db);
                        return;
                    }
                }
                catch (SocketException e)
                {
                    lastException = e;
                }
                catch (IOException e)
                {
                    lastException = e;
                }
            }
            throw lastException;
        }

        public static async Task<T> RetryScopedDatabaseAction<T>(IServiceProvider serviceProvider, Func<IEnoDatabase, Task<T>> function)
        {
            Exception lastException = null;
            for (int i = 0; i < DATABASE_RETRIES; i++)
            {
                try
                {
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<IEnoDatabase>();
                        return await function(db);
                    }
                }
                catch (SocketException e)
                {
                    lastException = e;
                }
                catch (IOException e)
                {
                    lastException = e;
                }
            }
            throw lastException;
        }

        public static List<T> DrainQueue<T>(ConcurrentQueue<T> queue, int max)
        {
            var drains = new List<T>(max);
            while (drains.Count < max)
            {
                if (queue.TryDequeue(out var result))
                {
                    drains.Add(result);
                }
                else
                {
                    break;
                }
            }
            return drains;
        }

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


        internal static string GenerateNoise()
        {
            var noiseContent = new byte[sizeof(int) * 3 + ENTROPY_IN_BYTES];
            ThreadSafeRandom.NextBytes(noiseContent);
            return UrlSafify(Convert.ToBase64String(noiseContent));
        }

        internal static byte[] GenerateFlagEntropy()
        {
            var entropy = new byte[ENTROPY_IN_BYTES];
            ThreadSafeRandom.NextBytes(entropy);
            return entropy;
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

        public static string UrlSafify(string input)
        {
            return input.Replace("+", "-").Replace("/", "_");
        }

        public static string UrlUnSafify(string input)
        {
            return input.Replace("-", "+").Replace("_", "/");
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
