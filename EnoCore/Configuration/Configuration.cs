namespace EnoCore.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using EnoCore.Models;

    public sealed record Configuration(
        string Title,
        long FlagValidityInRounds,
        int CheckedRoundsPerRound,
        int RoundLengthInSeconds,
        string DnsSuffix,
        int TeamSubnetBytesLength,
        string FlagSigningKey,
        FlagEncoding Encoding,
        List<ConfigurationTeam> Teams,
        List<ConfigurationTeam> ActiveTeams,
        List<ConfigurationService> Services,
        List<ConfigurationService> ActiveServices,
        Dictionary<long, string[]> Checkers);

    public sealed record ConfigurationTeam(
        long Id,
        string Name,
        string? Address,
        byte[] TeamSubnet,
        string? LogoUrl,
        string? CountryFlagUrl,
        bool Active);

    public sealed record ConfigurationService(
        long Id,
        string Name,
        int FlagsPerRound,
        int NoisesPerRound,
        int HavocsPerRound,
        int FlagStores,
        long WeightFactor,
        bool Active,
        string[] Checkers);
}
