using System;
using System.Buffers;
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.Extensions.Logging;

namespace EnoCore.Utils
{
    public enum FlagEncoding
    {
        Legacy,
        UTF8
    }
    public static class Misc
    {
        public static readonly string dataDirectory = $"..{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}";
        public const string SubmissionResultOk = "VALID: Flag accepted!\n";
        public const string SubmissionResultInvalid = "INVALID: You have submitted an invalid string!\n";
        public const string SubmissionResultDuplicate = "RESUBMIT: You have already sent this flag!\n";
        public const string SubmissionResultOwn = "OWNFLAG: This flag belongs to you!\n";
        public const string SubmissionResultOld = "OLD: You have submitted an old flag!\n";
        public const string SubmissionResultUnknownError = "ERROR: An unexpected error occured :(\n";
        public const string SubmissionResultInvalidSenderError = "ILLEGAL: Your IP address does not belong to any team's subnet!\n";
        public const string SubmissionResultSpamError = "SPAM: You should send 1 flag per line!\n";
        public const string SubmissionResultReallyUnknownError = "ERROR: An even more unexpected error occured :(\n";
    }
    ///
    /// code used from https://devblogs.microsoft.com/pfxteam/getting-random-numbers-in-a-thread-safe-way/
    public static class ThreadSafeRandom
    {
        private static readonly RNGCryptoServiceProvider _global = new RNGCryptoServiceProvider();
        [ThreadStatic]
        private static Random? _local;

        public static void NextBytes(byte[] array)
        {
            Random? inst = _local;
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
            Random? inst = _local;
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
}
