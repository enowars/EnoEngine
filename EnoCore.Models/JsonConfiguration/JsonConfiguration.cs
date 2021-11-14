namespace EnoCore.Models.JsonConfiguration;

public sealed record JsonConfiguration(
    string? Title,
    long FlagValidityInRounds,
    int CheckedRoundsPerRound,
    int RoundLengthInSeconds,
    string? DnsSuffix,
    int TeamSubnetBytesLength,
    string? FlagSigningKey,
    FlagEncoding Encoding,
    List<JsonConfigurationTeam>? Teams,
    List<JsonConfigurationService>? Services);

public record JsonConfigurationService(long Id,
    string? Name,
    int FlagsPerRoundMultiplier,
    int NoisesPerRoundMultiplier,
    int HavocsPerRoundMultiplier,
    long WeightFactor,
    string[]? Checkers,
    bool Active = true);

public record JsonConfigurationTeam(
    long Id,
    string? Name,
    string? Address,
    string? TeamSubnet,
    string? LogoUrl,
    string? CountryCode,
    bool Active = true);
