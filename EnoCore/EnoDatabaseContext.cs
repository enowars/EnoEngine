using EnoCore.Models;
using EnoCore.Models.Database;
using EnoCore.Models.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnoCore
{
    public class EnoDatabaseContextFactory : IDesignTimeDbContextFactory<EnoDatabaseContext>
    {
        public EnoDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<EnoDatabaseContext>();
            optionsBuilder.UseNpgsql(EnoCoreUtils.PostgresConnectionString, pgoptions => pgoptions.EnableRetryOnFailure());
            return new EnoDatabaseContext(optionsBuilder.Options);
        }
    }

    public class EnoDatabaseContext : DbContext
    {
        public DbSet<CheckerTask> CheckerTasks { get; set; }
        public DbSet<Flag> Flags { get; set; }
        public DbSet<Noise> Noises { get; set; }
        public DbSet<Havoc> Havocs { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<Round> Rounds { get; set; }
        public DbSet<RoundTeamServiceState> RoundTeamServiceStates { get; set; }
        public DbSet<SubmittedFlag> SubmittedFlags { get; set; }
        public DbSet<ServiceStats> ServiceStats { get; set; }
        public DbSet<ServiceStatsSnapshot> ServiceStatsSnapshots { get; set; }

        public EnoDatabaseContext(DbContextOptions<EnoDatabaseContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.CheckerTaskLaunchStatus);

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.StartTime);

            modelBuilder.Entity<SubmittedFlag>()
                .HasIndex(sf => new { sf.AttackerTeamId, sf.FlagId })
                .IsUnique();
        }

        private static async Task CalculateSLAScore(EnoDatabaseContext ctx, Service[] services, long currentRoundId, Team team, long newLatestSnapshotRoundId)
        {
            double slaScore = 0;
            double teamsCount = ctx.Teams.Count();
            foreach (var service in services)
            {
                var oldSnapshot = await ctx.ServiceStatsSnapshots
                    .Where(sss => sss.TeamId == team.Id)
                    .Where(sss => sss.ServiceId == service.Id)
                    .OrderByDescending(sss => sss.RoundId)
                    .Skip(1)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
                var oldSnapshotRoundId = oldSnapshot?.RoundId ?? 0;
                var oldSnapshotSlaCore = oldSnapshot?.ServiceLevelAgreementPoints ?? 0;

                double upsBetweenSnapshots = await ctx.RoundTeamServiceStates
                    .Where(rtss => rtss.GameRoundId > oldSnapshotRoundId)
                    .Where(rtss => rtss.GameRoundId <= newLatestSnapshotRoundId)
                    .Where(rtss => rtss.TeamId == team.Id)
                    .Where(rtss => rtss.ServiceId == service.Id)
                    .Where(rtss => rtss.Status == ServiceStatus.Ok)
                    .CountAsync();
                double recoversBetweenSnapshots = await ctx.RoundTeamServiceStates
                    .Where(rtss => rtss.GameRoundId > oldSnapshotRoundId)
                    .Where(rtss => rtss.GameRoundId <= newLatestSnapshotRoundId)
                    .Where(rtss => rtss.TeamId == team.Id)
                    .Where(rtss => rtss.ServiceId == service.Id)
                    .Where(rtss => rtss.Status == ServiceStatus.Recovering)
                    .CountAsync();

                double upsAfterNewSnapshot = await ctx.RoundTeamServiceStates
                    .Where(f => f.GameRoundId <= currentRoundId)
                    .Where(f => f.GameRoundId > newLatestSnapshotRoundId)
                    .Where(rtss => rtss.TeamId == team.Id)
                    .Where(rtss => rtss.ServiceId == service.Id)
                    .Where(rtss => rtss.Status == ServiceStatus.Ok)
                    .CountAsync();
                double recoversAfterNewSnapshot = await ctx.RoundTeamServiceStates
                    .Where(f => f.GameRoundId <= currentRoundId)
                    .Where(f => f.GameRoundId > newLatestSnapshotRoundId)
                    .Where(rtss => rtss.TeamId == team.Id)
                    .Where(rtss => rtss.ServiceId == service.Id)
                    .Where(rtss => rtss.Status == ServiceStatus.Recovering)
                    .CountAsync();

                double newSnapshotSlaScore = oldSnapshotSlaCore + (upsBetweenSnapshots + 0.5 * recoversBetweenSnapshots) * Math.Sqrt(teamsCount);
                double serviceSlaScore = newSnapshotSlaScore + (upsAfterNewSnapshot + 0.5 * recoversAfterNewSnapshot) * Math.Sqrt(teamsCount);
                slaScore += serviceSlaScore;
                (await ctx.ServiceStats
                    .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                    .SingleAsync()).ServiceLevelAgreementPoints = serviceSlaScore;

                if (newLatestSnapshotRoundId > oldSnapshotRoundId)
                {
                    (await ctx.ServiceStatsSnapshots
                        .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                        .Where(sss => sss.RoundId == newLatestSnapshotRoundId)
                        .SingleAsync()).ServiceLevelAgreementPoints = newSnapshotSlaScore;
                }
            }
            team.ServiceLevelAgreementPoints = slaScore;
            team.TotalPoints += slaScore;
        }

        private static async Task CalculateDefenseScore(EnoDatabaseContext ctx, Service[] services, long currentRoundId, Team team, long newLatestSnapshotRoundId)
        {
            double teamDefenseScore = 0;
            foreach (var service in services)
            {
                var oldSnapshot = await ctx.ServiceStatsSnapshots
                    .Where(sss => sss.TeamId == team.Id)
                    .Where(sss => sss.ServiceId == service.Id)
                    .OrderByDescending(sss => sss.RoundId)
                    .Skip(1)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
                var oldSnapshotRoundId = oldSnapshot?.RoundId ?? 0;
                var oldSnapshotLostDefPoints = oldSnapshot?.LostDefensePoints ?? 0;

                var ownedFlagsBetweenSnapshots = await ctx.Flags
                    .Where(f => f.GameRoundId > oldSnapshotRoundId)
                    .Where(f => f.GameRoundId <= newLatestSnapshotRoundId)
                    .Where(f => f.OwnerId == team.Id)
                    .Where(f => f.ServiceId == service.Id)
                    .ToArrayAsync();
                var ownedFlagsAfterNewSnapshot = await ctx.Flags
                    .Where(f => f.GameRoundId <= currentRoundId)
                    .Where(f => f.GameRoundId > newLatestSnapshotRoundId)
                    .Where(f => f.OwnerId == team.Id)
                    .Where(f => f.ServiceId == service.Id)
                    .ToArrayAsync();

                double newSnapshotDefenseScore = oldSnapshotLostDefPoints;
                foreach (var ownedFlag in ownedFlagsBetweenSnapshots)
                {
                    double allCapturesOfFlag = await ctx.SubmittedFlags
                        .Where(f => f.FlagId == ownedFlag.Id)
                        .CountAsync();
                    newSnapshotDefenseScore -= Math.Pow(allCapturesOfFlag, 0.75);
                }

                double serviceDefenseScore = newSnapshotDefenseScore;
                foreach (var ownedFlag in ownedFlagsAfterNewSnapshot)
                {
                    double allCapturesOfFlag = await ctx.SubmittedFlags
                        .Where(f => f.FlagId == ownedFlag.Id)
                        .CountAsync();
                    serviceDefenseScore -= Math.Pow(allCapturesOfFlag, 0.75);
                }

                teamDefenseScore += serviceDefenseScore;
                (await ctx.ServiceStats
                    .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                    .SingleAsync()).LostDefensePoints = serviceDefenseScore;

                if (newLatestSnapshotRoundId > oldSnapshotRoundId)
                {
                    (await ctx.ServiceStatsSnapshots
                        .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                        .Where(sss => sss.RoundId == newLatestSnapshotRoundId)
                        .SingleAsync()).LostDefensePoints = newSnapshotDefenseScore;
                }
            }
            team.LostDefensePoints = teamDefenseScore;
            team.TotalPoints += teamDefenseScore;
        }

        private static async Task CalculateOffenseScore(EnoDatabaseContext ctx, Service[] services, long currentRoundId, Team team, long newLatestSnapshotRoundId)
        {
            double offenseScore = 0;
            foreach (var service in services)
            {
                var oldSnapshot = await ctx.ServiceStatsSnapshots
                    .Where(sss => sss.TeamId == team.Id)
                    .Where(sss => sss.ServiceId == service.Id)
                    .OrderByDescending(sss => sss.RoundId)
                    .Skip(1)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
                var oldSnapshotRoundId = oldSnapshot?.RoundId ?? 0;
                var oldSnapshotAttackPoints = oldSnapshot?.AttackPoints ?? 0;

                var flagsCapturedByTeamBetweenSnapshots = await ctx.SubmittedFlags
                    .Where(f => f.AttackerTeamId == team.Id)
                    .Where(f => f.RoundId > oldSnapshotRoundId)
                    .Where(f => f.RoundId <= newLatestSnapshotRoundId)
                    .Include(f => f.Flag)
                    .Where(f => f.Flag.ServiceId == service.Id)
                    .ToArrayAsync();
                var flagsCapturedByTeamAfterNewSnapshot = await ctx.SubmittedFlags
                    .Where(f => f.RoundId <= currentRoundId)
                    .Where(f => f.RoundId > newLatestSnapshotRoundId)
                    .Where(f => f.AttackerTeamId == team.Id)
                    .Include(f => f.Flag)
                    .Where(f => f.Flag.ServiceId == service.Id)
                    .ToArrayAsync();

                double newSnapshotOffenseScore = oldSnapshotAttackPoints + flagsCapturedByTeamBetweenSnapshots.Length;
                foreach (var submittedFlag in flagsCapturedByTeamBetweenSnapshots)
                {
                    double capturesOfFlag = await ctx.Flags
                        .Where(f => f.Id == submittedFlag.FlagId)
                        .CountAsync();
                    newSnapshotOffenseScore += Math.Pow(1 / capturesOfFlag, 0.75);
                }
                double serviceOffenseScore = newSnapshotOffenseScore + flagsCapturedByTeamAfterNewSnapshot.Length;
                foreach (var submittedFlag in flagsCapturedByTeamAfterNewSnapshot)
                {
                    double capturesOfFlag = await ctx.Flags
                        .Where(f => f.Id == submittedFlag.FlagId)
                        .CountAsync();
                    newSnapshotOffenseScore += Math.Pow(1 / capturesOfFlag, 0.75);
                }

                offenseScore += serviceOffenseScore;
                (await ctx.ServiceStats
                    .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                    .SingleAsync()).AttackPoints = serviceOffenseScore;

                if (newLatestSnapshotRoundId > oldSnapshotRoundId)
                {
                    (await ctx.ServiceStatsSnapshots
                        .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                        .Where(sss => sss.RoundId == newLatestSnapshotRoundId)
                        .SingleAsync()).AttackPoints = newSnapshotOffenseScore;
                }
            }
            team.AttackPoints = offenseScore;
            team.TotalPoints += offenseScore;
        }

        
    }
}
