using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EnoCore.Models
{
    public class Flag
    {
        public long OwnerId { get; set; }
        public Team Owner { get; set; }
        public long ServiceId { get; set; }
        public Service Service { get; set; }
        public int RoundOffset { get; set; }
        public long RoundId { get; set; }
        public long Captures { get; set; }

        public virtual Round Round { get; set; }

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

        public static Flag FromString(string input)
        {
            try
            {
                if (!input.StartsWith("ENO"))
                {
                    return null;
                }
                var flag = input.Substring(3);
                var flagBytes = Convert.FromBase64String(EnoCoreUtils.UrlUnSafify(flag));

                var serviceId = BitConverter.ToInt32(flagBytes, 0);
                var roundOffset = BitConverter.ToInt32(flagBytes, sizeof(int));
                var ownerId = BitConverter.ToInt32(flagBytes, 2 * sizeof(int));
                var roundId = BitConverter.ToInt32(flagBytes, 3 * sizeof(int));
                var flagSignature = new ArraySegment<byte>(flagBytes, 4 * sizeof(int),
                                                           flagBytes.Length - (4 * sizeof(int)));
                using HMACSHA1 hmacsha1 = new HMACSHA1(EnoCoreUtils.FLAG_SIGNING_KEY);
                byte[] hash = hmacsha1.ComputeHash(flagBytes, 0, sizeof(int) * 4);
                if (!flagSignature.SequenceEqual(hash))
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
