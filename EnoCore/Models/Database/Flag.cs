using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EnoCore.Models
{
    public class Flag
    {
        public long Id { get; set; }
        public byte[] Entropy { get; set; }
        public long OwnerId { get; set; }
        public Team Owner { get; set; }
        public long ServiceId { get; set; }
        public Service Service { get; set; }
        public int RoundOffset { get; set; }
        public long GameRoundId { get; set; }
        public Round GameRound { get; set; }

        public override string ToString()
        {
            byte[] flagContent = new byte[sizeof(int) * 3 + EnoCoreUtils.ENTROPY_IN_BYTES];
            BitConverter.GetBytes(Id).CopyTo(flagContent, 0);
            BitConverter.GetBytes(OwnerId).CopyTo(flagContent, sizeof(int));
            BitConverter.GetBytes(GameRoundId).CopyTo(flagContent, 2 * sizeof(int));
            Entropy.CopyTo(flagContent, 3 * sizeof(int));

            using (HMACSHA1 hmacsha1 = new HMACSHA1(EnoCoreUtils.FLAG_SIGNING_KEY))
            {
                byte[] flagSignature = hmacsha1.ComputeHash(flagContent);
                byte[] flag = new byte[flagContent.Length + flagSignature.Length];
                flagContent.CopyTo(flag, 0);
                flagSignature.CopyTo(flag, flagContent.Length);
                return "ENO" + Convert.ToBase64String(flag);
            }
        }

        public static Flag FromString(string input)
        {
            try
            {
                var flag = input.Substring(3);
                var flagBytes = Convert.FromBase64String(flag);

                var id = BitConverter.ToInt32(flagBytes, 0);
                var ownerId = BitConverter.ToInt32(flagBytes, sizeof(int));
                var gameRoundId = BitConverter.ToInt32(flagBytes, sizeof(int) * 2);
                var flagSignature = new ArraySegment<byte>(flagBytes, sizeof(int) * 3 + EnoCoreUtils.ENTROPY_IN_BYTES,
                                                           flagBytes.Length - sizeof(int) * 3 - EnoCoreUtils.ENTROPY_IN_BYTES);
                using (HMACSHA1 hmacsha1 = new HMACSHA1(EnoCoreUtils.FLAG_SIGNING_KEY))
                {
                    byte[] hash = hmacsha1.ComputeHash(flagBytes, 0, sizeof(int) * 3 + EnoCoreUtils.ENTROPY_IN_BYTES);
                    if (!flagSignature.SequenceEqual(hash))
                    {
                        return null;
                    }
                    else
                    {
                        return new Flag()
                        {
                            Id = id,
                            OwnerId = ownerId,
                            GameRoundId = gameRoundId
                        };
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
