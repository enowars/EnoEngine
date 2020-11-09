using EnoCore.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnoCore.Models
{
    public record Configuration(string Title,
        long FlagValidityInRounds,
        int CheckedRoundsPerRound,
        int RoundLengthInSeconds,
        string DnsSuffix,
        int TeamSubnetBytesLength,
        string FlagSigningKey,
        FlagEncoding Encoding,
        List<ConfigurationTeam> Teams,
        List<ConfigurationService> Services,
        Dictionary<long, string[]> Checkers);

    public record ConfigurationTeam(long Id,
        string Name,
        string? Address,
        string TeamSubnet,
        string? LogoUrl,
        string? FlagUrl,
        bool Active);

    public record ConfigurationService(long Id,
        string Name,
        int FlagsPerRound,
        int NoisesPerRound,
        int HavocsPerRound,
        int FlagStores,
        long WeightFactor,
        bool Active,
        string[] Checkers);
}
