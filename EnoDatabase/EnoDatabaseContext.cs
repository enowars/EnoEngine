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

namespace EnoDatabase
{
    public class EnoDatabaseContextFactory : IDesignTimeDbContextFactory<EnoDatabaseContext>
    {
        public EnoDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<EnoDatabaseContext>();
            optionsBuilder.UseNpgsql(EnoDatabaseUtils.PostgresConnectionString, pgoptions => pgoptions.EnableRetryOnFailure());
            return new EnoDatabaseContext(optionsBuilder.Options);
        }
    }

    public class EnoDatabaseContext : DbContext
    {
#pragma warning disable CS8618
        public DbSet<CheckerTask> CheckerTasks { get; set; }
        //public DbSet<Flag> Flags { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<Round> Rounds { get; set; }
        public DbSet<RoundTeamServiceState> RoundTeamServiceStates { get; set; }
        public DbSet<SubmittedFlag> SubmittedFlags { get; set; }
        public DbSet<ServiceStats> ServiceStats { get; set; }
        public DbSet<ServiceStatsSnapshot> ServiceStatsSnapshots { get; set; }

        public EnoDatabaseContext(DbContextOptions<EnoDatabaseContext> options) : base(options) { }
#pragma warning restore CS8618

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //modelBuilder.Entity<Flag>()
            //    .HasKey(f => new { f.ServiceId, f.RoundId, f.OwnerId, f.RoundOffset });

            modelBuilder.Entity<SubmittedFlag>()
                .HasKey(sf => new { sf.FlagServiceId, sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset, sf.AttackerTeamId });

            modelBuilder.Entity<SubmittedFlag>()
                .HasIndex(sf => new { sf.FlagServiceId, sf.FlagRoundOffset, sf.Timestamp });

            modelBuilder.Entity<SubmittedFlag>()
                .HasOne(sf => sf.Flag)
                .WithMany(f => f.FlagSubmissions)
                .HasForeignKey(sf => new { sf.FlagServiceId, sf.FlagRoundId, sf.FlagOwnerId, sf.FlagRoundOffset });

            modelBuilder.Entity<RoundTeamServiceState>()
                .HasKey(rtss => new { rtss.ServiceId, rtss.TeamId, rtss.GameRoundId });

            modelBuilder.Entity<ServiceStatsSnapshot>()
                .HasKey(sss => new { sss.ServiceId, sss.RoundId, sss.TeamId });

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => new { ct.CurrentRoundId, ct.RelatedRoundId, ct.CheckerResult });

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => new { ct.CheckerTaskLaunchStatus, ct.StartTime });
        }
    }
}
