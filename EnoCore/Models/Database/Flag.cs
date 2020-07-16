using EnoCore.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

namespace EnoCore.Models.Database
{
    public enum FlagEncoding
    {
        Legacy,
        UTF8
    }

    /// <summary>
    /// PK: ServiceId, RoundId, OwnerId, RoundOffset
    /// </summary>
    public class Flag
    {
        private static readonly char[] ByteMap = new char[] { '̀', '́', '̂', '̃', '̄', '̅', '̆', '̇', '̈', '̉', '̊', '̋', '̌', '̍', '̎', '̏', '̐', '̑', '̒', '̓', '̔', '̕', '̖', '̗', '̘', '̙', '̚', '̛', '̜', '̝', '̞', '̟', '̠', '̡', '̢', '̣', '̤', '̥', '̦', '̧', '̨', '̩', '̪', '̫', '̬', '̭', '̮', '̯', '̰', '̱', '̲', '̳', '̴', '̵', '̶', '̷', '̸', '̹', '̺', '̻', '̼', '̽', '̾', '̿', '̀', '́', '͂', '̓', '̈́', 'ͅ', '͆', '͇', '͈', '͉', '͊', '͋', '͌', '͍', '͎', '͏', '͐', '͑', '͒', '͓', '͔', '͕', '͖', '͗', '͘', '͙', '͚', '͛', '͜', '͝', '͞', '͟', '͠', '͡', '͢', 'ͣ', 'ͤ', 'ͥ', 'ͦ', 'ͧ', 'ͨ', 'ͩ', 'ͪ', 'ͫ', 'ͬ', 'ͭ', 'ͮ', 'ͯ', '᪰', '᪱', '᪲', '᪳', '᪴', '᪵', '᪶', '᪷', '᪸', '᪹', '᪺', '᪻', '᪼', '᪽', '᪾', '᷀', '᷁', '᷂', '᷃', '᷄', '᷅', '᷆', '᷇', '᷈', '᷉', '᷊', '᷋', '᷌', '᷍', '᷎', '᷏', '᷐', '᷑', '᷒', 'ᷓ', 'ᷔ', 'ᷕ', 'ᷖ', 'ᷗ', 'ᷘ', 'ᷙ', 'ᷚ', 'ᷛ', 'ᷜ', 'ᷝ', 'ᷞ', 'ᷟ', 'ᷠ', 'ᷡ', 'ᷢ', 'ᷣ', 'ᷤ', 'ᷥ', 'ᷦ', 'ᷧ', 'ᷨ', 'ᷩ', 'ᷪ', 'ᷫ', 'ᷬ', 'ᷭ', 'ᷮ', 'ᷯ', 'ᷰ', 'ᷱ', 'ᷲ', 'ᷳ', 'ᷴ', '᷵', '᷻', '᷼', '᷽', '᷾', '᷿', '⃐', '⃑', '⃒', '⃓', '⃔', '⃕', '⃖', '⃗', '⃘', '⃙', '⃚', '⃛', '⃜', '⃝', '⃞', '⃟', '⃠', '⃡', '⃢', '⃣', '⃤', '⃥', '⃦', '⃧', '⃨', '⃩', '⃪', '⃫', '⃬', '⃭', '⃮', '⃯', '⃰', '︠', '︡', '︢', '︣', '︤', '︥', '︦', '︧', '︨', '︩', '︪', '︫', '︬', '︭', '︮', '︯', '゙', '゚', '⳯', '⳰', '⳱', '꣠', '꣡', '꣢', '꣣', '꣤', '꣥', '꣦', '꣧', '꣨', '꣩', '꣪', '꣫', '꣬', '꣭', '꣮', '꣯' };

        private static readonly string[] Flagprefix = new string[]
            {
                "🏳️‍🌈"
            };

#pragma warning disable CS8618
        public long OwnerId { get; set; }
        public Team Owner { get; set; }
        public long ServiceId { get; set; }
        public Service Service { get; set; }
        public int RoundOffset { get; set; }
        public long RoundId { get; set; }
        public Round Round { get; set; }
        public long Captures { get; set; }
        public virtual List<SubmittedFlag> FlagSubmissions { get; set; }
#pragma warning restore CS8618

        private string ToNormalString(byte[] signingKey)
        {
            Span<byte> flagContent = stackalloc byte[sizeof(int) * 4];
            BitConverter.TryWriteBytes(flagContent, (int)ServiceId);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int)), RoundOffset);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int) * 2), (int)OwnerId);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int) * 3), (int)RoundId);

            using HMACSHA1 hmacsha1 = new HMACSHA1(signingKey);
            Span<byte> flagSignature = stackalloc byte[hmacsha1.HashSize / 8];
            hmacsha1.TryComputeHash(flagContent, flagSignature, out var _);
            Span<byte> flagBytes = stackalloc byte[flagContent.Length + flagSignature.Length];
            flagContent.CopyTo(flagBytes);
            flagSignature.CopyTo(flagBytes.Slice(flagContent.Length));
            return "ENO" + Convert.ToBase64String(flagBytes);
        }
        private string ToUtfString(byte[] signingKey)
        {
            Span<byte> flagContent = stackalloc byte[sizeof(int) * 4];
            BitConverter.TryWriteBytes(flagContent, (int)ServiceId);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int)), RoundOffset);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int) * 2), (int)OwnerId);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int) * 3), (int)RoundId);

            using HMACSHA1 hmacsha1 = new HMACSHA1(signingKey);
            Span<byte> flagSignature = stackalloc byte[hmacsha1.HashSize / 8];
            hmacsha1.TryComputeHash(flagContent, flagSignature, out var _);
            Span<byte> flagBytes = stackalloc byte[flagContent.Length + flagSignature.Length];
            flagContent.CopyTo(flagBytes);
            flagSignature.CopyTo(flagBytes.Slice(flagContent.Length));
            Bytes2Dia(flagBytes, out var flagString, out var bytesWritten);
            return Flagprefix[0] + flagString;
        }
        private static void Bytes2Dia(ReadOnlySpan<byte> b, out string s, out int bytesWritten)
        {
            bytesWritten = 0;
            var pattern = new string[4] { "W", "A", "R", "S" };
            //var dbgstr = "";
            //var lstr = new int[4] { 0, 0, 0, 0 };
            foreach (byte c in b)
            {
                pattern[bytesWritten % 4] += ByteMap[c];
                bytesWritten++;
                //dbgstr += $"({bytesWritten}, {c}, {ByteMap[c]}, {Encoding.UTF8.GetByteCount(ByteMap[c].ToString())})";
                //lstr[bytesWritten % 4] += Encoding.UTF8.GetByteCount(ByteMap[c].ToString());
            }
            s = pattern[0] + pattern[1] + pattern[2] + pattern[3];
            /*
            var l0 = pattern[0].Length;
            var l1 = pattern[1].Length;
            var l2 = pattern[2].Length;
            var l3 = pattern[3].Length;
            string dbg = $"Bytes2Dia:{s.Length}||{Encoding.UTF8.GetByteCount(s)}||{l0}|{l1}|{l2}|{l3}||{lstr[0]}|{lstr[1]}|{lstr[2]}|{lstr[3]}"; 
            Console.WriteLine(dbgstr);
            Console.WriteLine(dbg);  */
        }
        private static bool Getsinglebyte(char s, out byte b)
        {
            for (int i = 0; i < 256; i++)
                if (ByteMap[i] == s)
                {
                    b = (byte)i;
                    return true;
                }
            b = 0;
            return false;
        }
        private static bool Dia2bytes(string s, Span<byte> b, out int bytesWritten)
        {
            bytesWritten = 0;
            var pattern = new string[4] { "W", "A", "R", "S" };
            var splitted = s.Split(pattern, StringSplitOptions.RemoveEmptyEntries);
            /*
            var lg = s.Length;
            var l0 = splitted[0].Length;
            var l1 = splitted[1].Length;
            var l2 = splitted[2].Length;
            var l3 = splitted[3].Length;
            string dbg = $"Dia2Bytes:{lg}||{l0}|{l1}|{l2}|{l3}|";
            Console.WriteLine(dbg);  */
            while (true)
            {
                var element = splitted[bytesWritten % 4].ElementAtOrDefault((int)bytesWritten / 4);
                if (element == '\0')
                    return true;
                if (!Getsinglebyte(element, out b[bytesWritten]))
                    return false;
                bytesWritten++;
            }
        }
        public string ToString(byte[] signingKey, FlagEncoding encoding)
        {
            return encoding switch
            {
                FlagEncoding.Legacy => this.ToNormalString(signingKey),
                FlagEncoding.UTF8 => this.ToUtfString(signingKey),
                _ => throw new NotImplementedException("FlagEncoding not implemented"),
            };
        }
        public static Flag? Parse(ReadOnlySequence<byte> line, byte[] signingKey, FlagEncoding encoding, ILogger logger)
        {
            return encoding switch
            {
                FlagEncoding.Legacy => ParseNormal(line, signingKey),
                FlagEncoding.UTF8 => ParseUtf(line, signingKey, logger),
                _ => throw new NotImplementedException("FlagEncoding not implemented"),
            };
        }
        private static Flag? ParseUtf(ReadOnlySequence<byte> line, byte[] signingKey, ILogger logger)
        {
            try
            {
                if (line.Length < 36) return null;
                Span<byte> baseBytes = stackalloc byte[(int)line.Length - Encoding.UTF8.GetByteCount(Flagprefix[0])]; // Raw input
                Span<byte> flagBytes = stackalloc byte[(int)line.Length];   // Decoded bytes
                Span<byte> computedSignature = stackalloc byte[20];         // HMACSHA1 output is always 20 bytes                           
                //Base64.DecodeFromUtf8(base64Bytes, flagBytes, out var _, out var flagLength);   // Base64-decode the flag into flagBytes
                Span<byte> bytes = stackalloc byte[36];
                line.Slice(Encoding.UTF8.GetByteCount(Flagprefix[0])).CopyTo(baseBytes);
                string flagstring = Encoding.UTF8.GetString(baseBytes);
                if (!Dia2bytes(flagstring, flagBytes, out var flagLength)) return null;
                // Deconstruct the flag
                var serviceId = BinaryPrimitives.ReadInt32LittleEndian(flagBytes);
                var roundOffset = BinaryPrimitives.ReadInt32LittleEndian(flagBytes.Slice(4, 4));
                var ownerId = BinaryPrimitives.ReadInt32LittleEndian(flagBytes.Slice(8, 4));
                var roundId = BinaryPrimitives.ReadInt32LittleEndian(flagBytes.Slice(12, 4));
                var flagSignature = flagBytes[16..flagLength];

                // Compute the hmac
                using HMACSHA1 hmacsha1 = new HMACSHA1(signingKey);
                hmacsha1.TryComputeHash(flagBytes.Slice(0, 16), computedSignature, out var _);

                // Showtime!
                if (!flagSignature.SequenceEqual(computedSignature))
                {
                    return null;
                }
                else
                {
                    return new Flag()
                    {
                        ServiceId = serviceId,
                        OwnerId = ownerId,
                        RoundId = roundId,
                        RoundOffset = roundOffset
                    };
                }
            }
            catch (Exception e)
            {
                logger.LogError(e.ToFancyString());
                return null;
            }
        }

        private static Flag? ParseNormal(ReadOnlySequence<byte> line, byte[] signingKey)
        {
            try
            {
                Span<byte> base64Bytes = stackalloc byte[(int)line.Length]; // Raw input
                Span<byte> flagBytes = stackalloc byte[(int)line.Length];   // Decoded bytes
                Span<byte> computedSignature = stackalloc byte[20];         // HMACSHA1 output is always 20 bytes

                line.Slice(3).CopyTo(base64Bytes);                                              // Copy ROS to stack-alloced buffer
                Base64.DecodeFromUtf8(base64Bytes, flagBytes, out var _, out var flagLength);   // Base64-decode the flag into flagBytes

                // Deconstruct the flag
                var serviceId = BinaryPrimitives.ReadInt32LittleEndian(flagBytes);
                var roundOffset = BinaryPrimitives.ReadInt32LittleEndian(flagBytes.Slice(4, 4));
                var ownerId = BinaryPrimitives.ReadInt32LittleEndian(flagBytes.Slice(8, 4));
                var roundId = BinaryPrimitives.ReadInt32LittleEndian(flagBytes.Slice(12, 4));
                var flagSignature = flagBytes[16..flagLength];

                // Compute the hmac
                using HMACSHA1 hmacsha1 = new HMACSHA1(signingKey);
                hmacsha1.TryComputeHash(flagBytes.Slice(0, 16), computedSignature, out var _);

                // Showtime!
                if (!flagSignature.SequenceEqual(computedSignature))
                {
                    return null;
                }
                else
                {
                    return new Flag()
                    {
                        ServiceId = serviceId,
                        OwnerId = ownerId,
                        RoundId = roundId,
                        RoundOffset = roundOffset
                    };
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
