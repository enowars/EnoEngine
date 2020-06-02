using EnoCore.Models.Database;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;

namespace EnoCore.Models
{
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
#pragma warning restore CS8618
        private string ToUtfString()
        {
            Span<byte> flagContent = stackalloc byte[sizeof(int) * 4];
            BitConverter.TryWriteBytes(flagContent, (int)ServiceId);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int)), RoundOffset);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int) * 2), (int)OwnerId);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int) * 3), (int)RoundId);

            using HMACSHA1 hmacsha1 = new HMACSHA1(EnoCoreUtils.FLAG_SIGNING_KEY);
            Span<byte> flagSignature = stackalloc byte[hmacsha1.HashSize + flagContent.Length];
            hmacsha1.TryComputeHash(flagContent, flagSignature, out var _);
            Span<byte> flagBytes = stackalloc byte[flagContent.Length + flagSignature.Length];
            flagContent.CopyTo(flagBytes);
            flagSignature.CopyTo(flagBytes.Slice(flagContent.Length));
            return Flagprefix[ThreadSafeRandom.Next(Flagprefix.Length)] + Bytes2dia(flagBytes);
        }
        private string Bytes2dia(Span<byte> s)
        {
            string result = "";
            string[] b = new string[4] { "W", "A", "R", "S" };
            int i = 0;
            foreach (byte c in s)
            {
                b[i%4] += ByteMap[c];
                i++;
            }
            result = b[0] + b[1] + b[2] + b[3];
            return result;
        }
        private string ToNormalString()
        {
            Span<byte> flagContent = stackalloc byte[sizeof(int) * 4];
            BitConverter.TryWriteBytes(flagContent, (int)ServiceId);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int)), RoundOffset);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int) * 2), (int)OwnerId);
            BitConverter.TryWriteBytes(flagContent.Slice(sizeof(int) * 3), (int)RoundId);

            using HMACSHA1 hmacsha1 = new HMACSHA1(EnoCoreUtils.FLAG_SIGNING_KEY);
            Span<byte> flagSignature = stackalloc byte[hmacsha1.HashSize + flagContent.Length];
            hmacsha1.TryComputeHash(flagContent, flagSignature, out var _);
            Span<byte> flagBytes = stackalloc byte[flagContent.Length + flagSignature.Length];
            flagContent.CopyTo(flagBytes);
            flagSignature.CopyTo(flagBytes.Slice(flagContent.Length));
            return "ENO" + EnoCoreUtils.UrlSafify(Convert.ToBase64String(flagBytes));
        }
        public override string ToString()
        {
            return ToUtfString();
        }

        public static Flag? Parse(ReadOnlySequence<byte> line)
        {
            try
            {
                Span<byte> base64Bytes = stackalloc byte[(int)line.Length]; // Raw input
                Span<byte> flagBytes = stackalloc byte[(int)line.Length];   // Decoded bytes
                Span<byte> computedSignature = stackalloc byte[20];         // HMACSHA1 output is always 20 bytes
                line.Slice(3).CopyTo(base64Bytes);                                              // Copy ROS to stack-alloced buffer
                EnoCoreUtils.UrlUnSafify(base64Bytes);                                          // Do replacement magic
                Base64.DecodeFromUtf8(base64Bytes, flagBytes, out var _, out var flagLength);   // Base64-decode the flag into flagBytes
                // Deconstruct the flag
                var serviceId = BinaryPrimitives.ReadInt32BigEndian(flagBytes);
                var roundOffset = BinaryPrimitives.ReadInt32BigEndian(flagBytes.Slice(4, 4));
                var ownerId = BinaryPrimitives.ReadInt32BigEndian(flagBytes.Slice(8, 4));
                var roundId = BinaryPrimitives.ReadInt32BigEndian(flagBytes.Slice(12, 4));
                var flagSignature = flagBytes[16..flagLength];

                // Compute the hmac
                using HMACSHA1 hmacsha1 = new HMACSHA1(EnoCoreUtils.FLAG_SIGNING_KEY);
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
