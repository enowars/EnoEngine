namespace EnoCore.Scoreboard
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using EnoCore.Models;

    public record Scoreboard(
        long CurrentRound,
        string? StartTimestamp,
        double? StartTimeEpoch,
        string? EndTimestamp,
        double? EndTimeEpoch,
        ScoreboardService[] Services,
        ScoreboardTeam[] Teams);

    public record ScoreboardService(
        long ServiceId,
        string ServiceName,
        long MaxStores,
        ScoreboardFirstBlood[] FirstBloods);

    public record ScoreboardFirstBlood(
        long TeamId,
        string Timestamp,
        double TimeEpoch,
        long RoundId,
        string? StoreDescription,
        long StoreIndex);

    public record ScoreboardTeam(
        string Name,
        long TeamId,
        double TotalPoints,
        double AttackPoints,
        double LostDefensePoints,
        double ServiceLevelAgreementPoints,
        ScoreboardTeamDetails[] ServiceDetails);

    public record ScoreboardTeamDetails(
        long ServiceId,
        double AttackPoints,
        double LostDefensePoints,
        double ServiceLevelAgreementPoints,
        ServiceStatus ServiceStatus,
        string? Message);
}
