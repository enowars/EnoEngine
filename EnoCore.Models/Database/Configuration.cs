namespace EnoCore.Models.Database;

public sealed record Configuration
{
    public Configuration(
        long id,
        string title,
        long flagValidityInRounds,
        int checkedRoundsPerRound,
        int roundLengthInSeconds,
        string dnsSuffix,
        int teamSubnetBytesLength,
        byte[] flagSigningKey,
        FlagEncoding encoding)
    {
        this.Id = id;
        this.Title = title;
        this.FlagValidityInRounds = flagValidityInRounds;
        this.CheckedRoundsPerRound = checkedRoundsPerRound;
        this.RoundLengthInSeconds = roundLengthInSeconds;
        this.DnsSuffix = dnsSuffix;
        this.TeamSubnetBytesLength = teamSubnetBytesLength;
        this.FlagSigningKey = flagSigningKey;
        this.Encoding = encoding;
    }

    public long Id { get; set; }

    public string Title { get; set; }

    public long FlagValidityInRounds { get; set; }

    public int CheckedRoundsPerRound { get; set; }

    public int RoundLengthInSeconds { get; set; }

    public string DnsSuffix { get; set; }

    public int TeamSubnetBytesLength { get; set; }

    public byte[] FlagSigningKey { get; set; }

    public FlagEncoding Encoding { get; set; }
}
