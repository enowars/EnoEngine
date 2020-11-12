namespace EnoDatabase
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using EnoCore.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;

    public class EnoDatabaseContext : DbContext
    {
#pragma warning disable SA1516 // Elements should be separated by blank line
        public const int DatabaseRetries = 500;
#pragma warning disable CS8618
        public EnoDatabaseContext(DbContextOptions<EnoDatabaseContext> options)
            : base(options)
        {
        }

        public static string PostgresDomain => Environment.GetEnvironmentVariable("DATABASE_DOMAIN") ?? "localhost";
        public static string PostgresConnectionString => $@"Server={PostgresDomain};Port=5432;Database=EnoDatabase;User Id=docker;Password=docker;Timeout=15;SslMode=Disable;";
        public DbSet<CheckerTask> CheckerTasks { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<Round> Rounds { get; set; }
        public DbSet<RoundTeamServiceStatus> RoundTeamServiceStatus { get; set; }
        public DbSet<SubmittedFlag> SubmittedFlags { get; set; }
        public DbSet<TeamServicePoints> TeamServicePoints { get; set; }
        public DbSet<TeamServicePointsSnapshot> TeamServicePointsSnapshot { get; set; }
#pragma warning restore SA1516 // Elements should be separated by blank line
#pragma warning restore CS8618

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SubmittedFlag>()
                .HasKey(sf => new { sf.FlagServiceId, sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset, sf.AttackerTeamId });

            modelBuilder.Entity<SubmittedFlag>()
                .HasIndex(sf => new { sf.FlagServiceId, sf.FlagRoundOffset, sf.Timestamp });

            modelBuilder.Entity<TeamServicePoints>()
                .HasKey(tsp => new { tsp.TeamId, tsp.ServiceId });

            modelBuilder.Entity<RoundTeamServiceStatus>()
                .HasKey(rtss => new { rtss.ServiceId, rtss.TeamId, rtss.GameRoundId });

            modelBuilder.Entity<TeamServicePointsSnapshot>()
                .HasKey(sss => new { sss.ServiceId, sss.RoundId, sss.TeamId });

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => new { ct.CurrentRoundId, ct.RelatedRoundId, ct.CheckerResult });

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => new { ct.CheckerTaskLaunchStatus, ct.StartTime });
        }
    }
}
