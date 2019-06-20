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

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.CurrentRoundId);

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.RelatedRoundId);

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.CheckerResult);

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.TeamId);

            modelBuilder.Entity<SubmittedFlag>()
                .HasIndex(sf => new { sf.AttackerTeamId, sf.FlagId })
                .IsUnique();

            modelBuilder.Entity<RoundTeamServiceState>()
                .HasIndex(rtss => rtss.Status);
        }
    }
}
