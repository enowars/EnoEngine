using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models
{
    public class EnoScoreboardFirstblood
    {
        public readonly DateTime FirstTime;
        public long TeamId { get; private set; }
        public string Timestamp { get => FirstTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"); }
        public double TimeEpoch { get => FirstTime.Subtract(DateTime.UnixEpoch).TotalSeconds; }
        public long RoundId { get; private set; }
        public string? StoreDescription { get; private set; }
        public long StoreIndex { get; private set; }
        public EnoScoreboardFirstblood(DateTime time, long teamId, long roundId, string? storeDescription, long storeIndex)
        {
            FirstTime = time;
            TeamId = teamId;
            RoundId = roundId;
            StoreDescription = storeDescription;
            StoreIndex = storeIndex;
        }
    }
}
