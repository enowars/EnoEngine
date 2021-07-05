namespace EnoCore.Models.JsonConfiguration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore.Models;
    using EnoCore.Models.CheckerApi;
    using EnoCore.Models.Database;

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
}
