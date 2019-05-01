using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EnoCore
{
    public class EnoCoreUtils
    {
        private static readonly RNGCryptoServiceProvider Random = new RNGCryptoServiceProvider();
        private static readonly int ENTROPY_IN_BYTES = 8;
        private static readonly int IPV4_SUBNET_SIZE = 24;
        private static readonly int IPV6_SUBNET_SIZE = 64;
        private static readonly byte[] FLAG_SIGNING_KEY = Encoding.ASCII.GetBytes("suchasecretstornkkeytheywillneverguess");
        private static readonly byte[] NOISE_SIGNING_KEY = Encoding.ASCII.GetBytes("anotherstrenksecrettheyvref24tr");


        public static CheckerResult ParseCheckerResult(string result)
        {
            switch (result) {
                case "INTERNAL_ERROR":
                    return CheckerResult.CheckerError;
                case "OK":
                    return CheckerResult.Ok;
                case "ENOWORKS":
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
    }
}
