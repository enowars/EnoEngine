namespace EnoCore.Scoreboard
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using EnoCore.Models;

    public record ScoreboardInfo(
        string? DnsSuffix,
        ScoreboardService[] Services,
        ScoreboardInfoTeam[] Teams);

    public record ScoreboardInfoTeam(
        long TeamId,
        string TeamName,
        string? LogoUrl,
        string? CountryCode);

    public record Scoreboard(
        long CurrentRound,
        string? StartTimestamp,
        string? EndTimestamp,
        string? DnsSuffix,
        ScoreboardService[] Services,
        ScoreboardTeam[] Teams);

    public record ScoreboardTeam(
        string TeamName,
        long TeamId,
        string? LogoUrl,
        string? CountryCode,
        double TotalScore,
        double AttackScore,
        double DefenseScore,
        double ServiceLevelAgreementScore,
        ScoreboardTeamServiceDetails[] ServiceDetails);

    public record ScoreboardTeamServiceDetails(
        long ServiceId,
        double AttackScore,
        double DefenseScore,
        double ServiceLevelAgreementScore,
        ServiceStatus ServiceStatus,
        string? Message);

    public record ScoreboardService(
        long ServiceId,
        string ServiceName,
        long FlagVariants,
        ScoreboardFirstBlood[] FirstBloods);

    public record ScoreboardFirstBlood(
        long TeamId,
        string TeamName,
        string Timestamp,
        long RoundId,
        long FlagVariantId);
}
