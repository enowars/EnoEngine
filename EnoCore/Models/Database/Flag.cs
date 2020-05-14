using EnoCore.Models.Database;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EnoCore.Models
{
    /// <summary>
    /// PK: ServiceId, RoundId, OwnerId, RoundOffset
    /// </summary>
    public class Flag
    {
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

        public override string ToString()
        {
            byte[] flagContent = new byte[sizeof(int) * 4];
            BitConverter.GetBytes((int)ServiceId).CopyTo(flagContent, 0);
            BitConverter.GetBytes((int)RoundOffset).CopyTo(flagContent, sizeof(int));
            BitConverter.GetBytes((int)OwnerId).CopyTo(flagContent, 2 * sizeof(int));
            BitConverter.GetBytes((int)RoundId).CopyTo(flagContent, 3 * sizeof(int));

            using HMACSHA1 hmacsha1 = new HMACSHA1(EnoCoreUtils.FLAG_SIGNING_KEY);
            byte[] flagSignature = hmacsha1.ComputeHash(flagContent);
            byte[] flag = new byte[flagContent.Length + flagSignature.Length];
            flagContent.CopyTo(flag, 0);
            flagSignature.CopyTo(flag, flagContent.Length);
            return "ENO" + EnoCoreUtils.UrlSafify(Convert.ToBase64String(flag));
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
