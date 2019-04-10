using EnoCore.Models;
using Microsoft.EntityFrameworkCore;
using EnoCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EnoCore.Models.Json;
using EnoCore.Models.Database;
using Microsoft.Extensions.Logging;

namespace EnoCore
{
    public enum FlagSubmissionResult
    {
        Ok,
        Invalid,
        Duplicate,
        Own,
        Old,
        UnknownError,
        InvalidSenderError
    }

    public struct DBInitializationResult
    {
        public bool Success;
        public string ErrorMessage;
    }

    internal class EnoEngineDBContext : DbContext
    {
        private static readonly ILogger Logger = EnoCoreUtils.Loggers.CreateLogger<EnoEngineDBContext>();
        public DbSet<CheckerTask> CheckerTasks { get; set; }
        public DbSet<Flag> Flags { get; set; }
        public DbSet<Noise> Noises { get; set; }
        public DbSet<Havok> Havoks { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<Round> Rounds { get; set; }
        public DbSet<RoundTeamServiceState> RoundTeamServiceStates { get; set; }
        public DbSet<SubmittedFlag> SubmittedFlags { get; set; }
        public DbSet<ServiceStats> ServiceStats { get; set; }
        public DbSet<CheckerLogMessage> Logs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var databaseDomain = Environment.GetEnvironmentVariable("DATABASE_DOMAIN") ?? "localhost";
            optionsBuilder.UseNpgsql($@"Server={databaseDomain};Port=5432;Database=EnoDatabase;User Id=docker;Password=docker;Timeout=15;SslMode=Disable;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.Id);

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.CheckerTaskLaunchStatus);

            modelBuilder.Entity<Flag>()
                .HasIndex(f => f.Id);

            modelBuilder.Entity<Noise>()
                .HasIndex(f => f.Id);

            modelBuilder.Entity<Service>()
                .HasIndex(s => s.Id);

            modelBuilder.Entity<Team>()
                .HasIndex(t => t.Id);

            modelBuilder.Entity<Round>()
                .HasIndex(r => r.Id);

            modelBuilder.Entity<RoundTeamServiceState>()
                .HasIndex(rtss => rtss.Id);

            modelBuilder.Entity<SubmittedFlag>()
                .HasIndex(sf => sf.Id);

            modelBuilder.Entity<ServiceStats>()
                .HasIndex(ss => ss.Id);

            modelBuilder.Entity<SubmittedFlag>()
                .HasIndex(sf => new { sf.AttackerTeamId, sf.FlagId })
                .IsUnique();

            modelBuilder.Entity<CheckerLogMessage>()
                .HasIndex(ss => ss.Id);

            modelBuilder.Entity<CheckerLogMessage>()
                .HasIndex(ss => ss.Timestamp);

            modelBuilder.Entity<CheckerLogMessage>()
                .HasIndex(ss => ss.RelatedTaskId);
        }
    }

    public class EnoDatabase
    {
        private static readonly ILogger Logger = EnoCoreUtils.Loggers.CreateLogger<EnoDatabase>();

        public static DBInitializationResult ApplyConfig(JsonConfiguration config)
        {
            Logger.LogTrace("ApplyConfig()");
            if (config.RoundLengthInSeconds <= 0)
                return new DBInitializationResult
                {
                    Success = false,
                    ErrorMessage = "RoundLengthInSeconds must not be 0"
                };

            if (config.CheckedRoundsPerRound <= 0)
                return new DBInitializationResult
                {
                    Success = false,
                    ErrorMessage = "CheckedRoundsPerRound must not be 0"
                };

            if (config.FlagValidityInRounds < 1)
                return new DBInitializationResult
                {
                    Success = false,
                    ErrorMessage = "CheckedRoundsPerRound must not be 0"
                };

            Migrate();
            using (var ctx = new EnoEngineDBContext())
            {
                var migrationResult = FillDatabase(ctx, config);
                if (migrationResult.Success)
                    ctx.SaveChanges();
                return migrationResult;
            }
        }

        public static async Task<FlagSubmissionResult> InsertSubmittedFlag(string flag, string attackerSubmissionAddress, long flagValidityInRounds)
        {
            while(true)
            {
                try
                {
                    using (var ctx = new EnoEngineDBContext())
                    {
                        var dbFlag = await ctx.Flags
                            .Where(f => f.StringRepresentation == flag)
                            .Include(f => f.Owner)
                            .AsNoTracking()
                            .SingleOrDefaultAsync();
                        var dbAttackerTeam = await ctx.Teams
                            .Where(t => t.VulnboxAddress == attackerSubmissionAddress || t.GatewayAddress == attackerSubmissionAddress)
                            .AsNoTracking()
                            .SingleOrDefaultAsync();
                        var currentRound = await ctx.Rounds
                            .AsNoTracking()
                            .LastOrDefaultAsync();

                        if (dbFlag == null)
                            return FlagSubmissionResult.Invalid;
                        if (dbAttackerTeam == null)
                            return FlagSubmissionResult.InvalidSenderError;
                        if (dbFlag.Owner.TeamId == dbAttackerTeam.TeamId)
                            return FlagSubmissionResult.Own;
                        if (dbFlag.GameRoundId < currentRound.Id - flagValidityInRounds)
                            return FlagSubmissionResult.Old;

                        var submittedFlag = await ctx.SubmittedFlags
                            .Where(f => f.FlagId == dbFlag.Id && f.AttackerTeamId == dbAttackerTeam.Id)
                            .SingleOrDefaultAsync();

                        if (submittedFlag != null)
                        {
                            submittedFlag.SubmissionsCount += 1;
                            ctx.SaveChanges();
                            return FlagSubmissionResult.Duplicate;
                        }
                        ctx.SubmittedFlags.Add(new SubmittedFlag()
                        {
                            FlagId = dbFlag.Id,
                            AttackerTeamId = dbAttackerTeam.Id,
                            SubmissionsCount = 1,
                            RoundId = currentRound.Id
                        });
                        Console.WriteLine($"Team {dbAttackerTeam.Id} submitted a flag!");
                        ctx.SaveChanges();
                        return FlagSubmissionResult.Ok;
                    }
                }
                catch (DbUpdateException) {}
            }
            
        }

        public static void Migrate()
        {
            using (var ctx = new EnoEngineDBContext())
            {
                var pendingMigrations = ctx.Database.GetPendingMigrations().Count();
                if (pendingMigrations > 0)
                {
                    Logger.LogInformation($"Applying {pendingMigrations} migration(s)");
                    ctx.Database.Migrate();
                    ctx.SaveChanges();
                }
            }
        }

        public static (Round, IEnumerable<Flag>, IEnumerable<Noise>) CreateNewRound(DateTime begin, DateTime q2, DateTime q3, DateTime q4, DateTime end)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                var round = new Round()
                {
                    Begin = begin,
                    Quarter2 = q2,
                    Quarter3 = q3,
                    Quarter4 = q4,
                    End = end
                };
                ctx.Rounds.Add(round);
                var flags = GenerateFlagsForRound(ctx, round);
                var noises = GenerateNoisesForRound(ctx, round);
                ctx.SaveChanges();
                return (round, flags, noises);
            }
        }

        public static async Task InsertCheckerTasks(List<CheckerTask> tasks)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                ctx.AddRange(tasks);
                await ctx.SaveChangesAsync();
            }
        }

        public static async Task<List<CheckerTask>> RetrievePendingCheckerTasks(int maxAmount)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                var tasks = await ctx.CheckerTasks
                    .Where(t => t.CheckerTaskLaunchStatus == CheckerTaskLaunchStatus.New)
                    .Take(maxAmount)
                    .ToListAsync();
                // TODO update launch status without delaying operation
                tasks.ForEach((t) => t.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Launched);
                await ctx.SaveChangesAsync();
                return tasks;
            }
        }

        private static IEnumerable<Flag> GenerateFlagsForRound(EnoEngineDBContext ctx, Round round)
        {
            IList<Flag> newFlags = new List<Flag>();
            var teams = ctx.Teams
                .ToArray();
            var services = ctx.Services
                .ToArray();
            foreach (var team in teams)
            {
                foreach (var service in services)
                {
                    for (int i = 0; i < service.FlagsPerRound; i++)
                    {
                        var flag = new Flag()
                        {
                            Owner = team,
                            StringRepresentation = "ENO" + EnoCoreUtils.RandomString(31) + "=",
                            Service = service,
                            RoundOffset = i,
                            GameRound = round
                        };
                        newFlags.Add(flag);
                    }
                }
            }
            ctx.Flags.AddRange(newFlags);
            return newFlags;
        }

        private static IEnumerable<Noise> GenerateNoisesForRound(EnoEngineDBContext ctx, Round round)
        {
            IList<Noise> newNoises = new List<Noise>();
            var teams = ctx.Teams
                .ToArray();
            var services = ctx.Services
                .ToArray();
            foreach (var team in teams)
            {
                foreach (var service in services)
                {
                    for (int i = 0; i < service.FlagsPerRound; i++)
                    {
                        var noise = new Noise()
                        {
                            Owner = team,
                            StringRepresentation = "ENO" + EnoCoreUtils.RandomString(31) + "=",
                            Service = service,
                            RoundOffset = i,
                            GameRound = round
                        };
                        newNoises.Add(noise);
                    }
                }
            }
            ctx.Noises.AddRange(newNoises);
            return newNoises;
        }

        public static async Task InsertRetrieveOldFlagsTasks(Round currentRound, int oldRoundsCount, int roundLengthInSeconds)
        {
            int quarterRound = roundLengthInSeconds / 4;
            using (var ctx = new EnoEngineDBContext())
            {
                var oldFlags = await ctx.Flags
                    .Where(f => f.GameRoundId + oldRoundsCount >= currentRound.Id)
                    .Where(f => f.GameRoundId != currentRound.Id)
                    .Include(f => f.Owner)
                    .Include(f => f.Service)
                    .AsNoTracking()
                    .ToArrayAsync();
                List<CheckerTask> oldFlagsCheckerTasks = new List<CheckerTask>(oldFlags.Count());
                double timeDiff = (double)quarterRound * 3 / oldFlags.Count();
                DateTime time = currentRound.Begin;
                foreach (var oldFlag in oldFlags)
                {
                    var task = new CheckerTask()
                    {
                        Address = oldFlag.Owner.VulnboxAddress,
                        MaxRunningTime = quarterRound,
                        Payload = oldFlag.StringRepresentation,
                        RelatedRoundId = oldFlag.GameRoundId,
                        CurrentRoundId = currentRound.Id,
                        StartTime = time,
                        TaskIndex = oldFlag.RoundOffset,
                        TaskType = "getflag",
                        TeamName = oldFlag.Owner.Name,
                        ServiceId = oldFlag.ServiceId,
                        TeamId = oldFlag.OwnerId,
                        ServiceName = oldFlag.Service.Name,
                        CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New
                    };
                    oldFlagsCheckerTasks.Add(task);
                    time = time.AddSeconds(timeDiff);
                }
                ctx.CheckerTasks.AddRange(oldFlagsCheckerTasks);
                await ctx.SaveChangesAsync();
            }
        }

        internal static EnoEngineScoreboard GetCurrentScoreboard()
        {
            using (var ctx = new EnoEngineDBContext())
            {
                var teams = ctx.Teams.AsNoTracking().OrderByDescending(t => t.TotalPoints).ToList();
                var round = ctx.Rounds.AsNoTracking().Last();
                var services = ctx.Services.AsNoTracking().ToList();
                var scoreboard = new EnoEngineScoreboard(round, services);
                foreach (var team in teams)
                {
                    var details = ctx.ServiceStats
                        .Where(ss => ss.TeamId == team.Id)
                        .AsNoTracking()
                        .ToList();
                    scoreboard.Teams.Add(new EnoEngineScoreboardEntry(team, details));
                }
                return scoreboard;
            }
        }

        private static DBInitializationResult FillDatabase(EnoEngineDBContext ctx, JsonConfiguration config)
        {
            var staleDbTeamIds = ctx.Teams.Select(t => t.TeamId).ToList();

            // insert (valid!) teams
            foreach (var team in config.Teams)
            {
                if (team.Name == null)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = "Team must not have a null name"
                    };

                if (team.VulnboxAddress == null)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = "Team vulnbox address must not be null"
                    };
                if (team.GatewayAddress == null)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = "Team gateway address must not be null"
                    };
                if (team.Id == 0)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = "Team must have a valid Id"
                    };

                // check if team is already present
                var dbTeam = ctx.Teams
                    .Where(t => t.TeamId == team.Id)
                    .SingleOrDefault();
                if (dbTeam == null)
                {
                    Logger.LogInformation($"New Team: {team.Name}({team.Id})");
                    ctx.Teams.Add(new Team()
                    {
                        VulnboxAddress = team.VulnboxAddress,
                        GatewayAddress = team.GatewayAddress,
                        Name = team.Name,
                        TeamId = team.Id
                    });
                }
                else
                {
                    dbTeam.VulnboxAddress = team.VulnboxAddress;
                    dbTeam.GatewayAddress = team.GatewayAddress;
                    dbTeam.Name = team.Name;
                    dbTeam.TeamId = team.Id;
                    staleDbTeamIds.Remove(team.Id);
                }
            }
            if (staleDbTeamIds.Count() > 0)
                return new DBInitializationResult
                {
                    Success = false,
                    ErrorMessage = $"Stale team in database: {staleDbTeamIds[0]}"
                };
            //insert (valid!) services
            long i = 1;
            var staleDbServiceIds = ctx.Services.Select(t => t.Id).ToList();
            foreach (var service in config.Services)
            {
                if (service.Name == null)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = "Service must have a valid name"
                    };

                if (service.FlagsPerRound < 1)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = $"Service {service.Name}: FlagsPerRound < 1"
                    };
                if (service.NoisesPerRound < 0)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = $"Service {service.Name}: NoisesPerRound < 0"
                    };
                if (service.RunsPerFlag < 1)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = $"Service {service.Name}: RunsPerFlag < 1"
                    };
                if (service.RunsPerNoise < 1)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = $"Service {service.Name}: RunsPerNoise < 1"
                    };
                if (service.RunsPerHavok < 0)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = $"Service {service.Name}: RunsPerHavok < 0"
                    };
                if (service.WeightFactor <= 0)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = $"Service {service.Name}: WeightFactor <= 0"
                    };
                if (service.FlagsPerRound < 1)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = $"Service {service.Name}: FlagsPerRound < 1"
                    };
                var dbService = ctx.Services
                    .Where(s => s.Id == i)
                    .SingleOrDefault();
                if (dbService == null)
                {
                    Logger.LogInformation($"New Service: {service.Name}");
                    ctx.Services.Add(new Service()
                    {
                        Name = service.Name,
                        FlagsPerRound = service.FlagsPerRound
                    });
                }
                else
                {
                    if (dbService.Name == service.Name)
                    {
                        staleDbServiceIds.Remove(dbService.Id);
                    }
                    else
                    {
                        return new DBInitializationResult()
                        {
                            Success = false,
                            ErrorMessage = $"Services in db and config diverge: ({dbService.Name} != {service.Name}"
                        };
                    }
                }
                i++;
            }
            if (staleDbServiceIds.Count() > 0)
            {
                return new DBInitializationResult
                {
                    Success = false,
                    ErrorMessage = $"Stale service in database: {staleDbServiceIds[0]}"
                };
            }

            ctx.SaveChanges(); // Save so that the services and teams receive proper IDs
            foreach (var service in ctx.Services)
            {
                foreach (var team in ctx.Teams)
                {
                    var stats = ctx.ServiceStats
                        .Where(ss => ss.TeamId == team.Id)
                        .Where (ss => ss.ServiceId == service.Id)
                        .SingleOrDefault();
                    if (stats == null)
                    {
                        ctx.ServiceStats.Add(new ServiceStats()
                        {
                            AttackPoints = 0,
                            LostDefensePoints = 0,
                            ServiceLevelAgreementPoints = 0,
                            Team = team,
                            Service = service,
                            Status = ServiceStatus.Down
                        });

                    }
                }
            }
            return new DBInitializationResult
            {
                Success = true
            };
        }

        public static async Task RecordServiceStates(long roundId)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                var round = await ctx.Rounds
                    .Where(r => r.Id == roundId)
                    .AsNoTracking()
                    .SingleAsync();

                var teams = await ctx.Teams.AsNoTracking().ToArrayAsync();
                var services = await ctx.Services.AsNoTracking().ToArrayAsync();
                foreach (var team in teams)
                {
                    foreach (var service in services)
                    {
                        var reportedStatus = ComputeServiceStatus(ctx, team, service, roundId);
                        var roundTeamServiceState = new RoundTeamServiceState()
                        {
                            FlagsCaptured = 0,
                            FlagsLost = 0,
                            GameRoundId = round.Id,
                            ServiceId = service.Id,
                            TeamId = team.Id,
                            Status = reportedStatus
                        };
                        ctx.RoundTeamServiceStates.Add(roundTeamServiceState);

                        var dbGlobalTeamServiceStats = ctx.ServiceStats
                           .Where(ss => ss.TeamId == team.TeamId && ss.ServiceId == service.Id)
                           .Single().Status = reportedStatus;
                    }
                }
                await ctx.SaveChangesAsync();
            }
        }

        public static void CalculatedAllPoints(long roundId)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                var services = ctx.Services.ToArray();
                var teams = ctx.Teams;
                foreach (var team in teams)
                {
                    CalculateTeamScore(ctx, services, roundId, team);
                    ctx.SaveChanges();
                }
            }
        }

        public static async Task UpdateTaskCheckerTaskResult(long checkerTaskId, CheckerResult checkerResult)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                var task = await ctx.CheckerTasks
                    .Where(ct => ct.Id == checkerTaskId)
                    .SingleAsync();
                task.CheckerResult = checkerResult;
                task.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Done;
                await ctx.SaveChangesAsync();
            }
        }

        private static void CalculateTeamScore(EnoEngineDBContext ctx, Service[] services, long currentRoundId, Team team)
        {
            team.TotalPoints = 0;
            CalculateOffenseScore(ctx, services, currentRoundId, team);
            CalculateDefenseScore(ctx, services, currentRoundId, team);
            CalculateSLAScore(ctx, services, currentRoundId, team);
        }

        private static void CalculateSLAScore(EnoEngineDBContext ctx, Service[] services, long currentRoundId, Team team)
        {
            double slaScore = 0;
            double teamsCount = ctx.Teams.Count();
            foreach (var service in services)
            {
                double ups = ctx.RoundTeamServiceStates
                    .Where(rtss => rtss.TeamId == team.Id)
                    .Where(rtss => rtss.ServiceId == service.Id)
                    .Where(rtss => rtss.Status == ServiceStatus.Ok)
                    .Count();

                double recovers = ctx.RoundTeamServiceStates
                    .Where(rtss => rtss.TeamId == team.Id)
                    .Where(rtss => rtss.ServiceId == service.Id)
                    .Where(rtss => rtss.Status == ServiceStatus.Recovering)
                    .Count();

                double serviceSlaScore = (ups + 0.5 * recovers) * Math.Sqrt(teamsCount);
                slaScore += serviceSlaScore;
                ctx.ServiceStats
                    .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                    .Single().ServiceLevelAgreementPoints = serviceSlaScore;
            }
            team.ServiceLevelAgreementPoints = slaScore;
            team.TotalPoints += slaScore;
            Logger.LogInformation($"Team {team.Name}: SLA={slaScore}");
        }

        private static void CalculateDefenseScore(EnoEngineDBContext ctx, Service[] services, long currentRoundId, Team team)
        {
            double teamDefenseScore = 0;
            foreach (var service in services)
            {
                double serviceDefenseScore = 0;
                var ownedFlags = ctx.Flags
                    .Where(f => f.OwnerId == team.Id)
                    .Where(f => f.ServiceId == service.Id);

                foreach (var ownedFlag in ownedFlags)
                {
                    double allCapturesOfFlag = ctx.SubmittedFlags
                        .Where(f => f.FlagId == ownedFlag.Id)
                        .Count();

                    serviceDefenseScore -= Math.Pow(allCapturesOfFlag, 0.75);
                }
                teamDefenseScore += serviceDefenseScore;
                ctx.ServiceStats
                    .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                    .Single().LostDefensePoints = serviceDefenseScore;
            }
            team.LostDefensePoints = teamDefenseScore;
            team.TotalPoints += teamDefenseScore;
            Logger.LogInformation($"Team {team.Name}: Defense={teamDefenseScore}");
        }

        private static void CalculateOffenseScore(EnoEngineDBContext ctx, Service[] services, long currentRoundId, Team team)
        {
            double offenseScore = 0;
            foreach (var service in services)
            {
                var flagsCapturedByTeam = ctx.SubmittedFlags
                    .Where(f => f.AttackerTeamId == team.Id)
                    .Include(f => f.Flag)
                    .Where(f => f.Flag.ServiceId == service.Id);

                double serviceOffenseScore = flagsCapturedByTeam.Count();
                foreach (var submittedFlag in flagsCapturedByTeam)
                {
                    double capturesOfFlag = ctx.Flags
                        .Where(f => f.Id == submittedFlag.FlagId)
                        .Count();
                    serviceOffenseScore += Math.Pow(1 / capturesOfFlag, 0.75);
                }

                offenseScore += serviceOffenseScore;
                var dbGlobalTeamServiceStats = ctx.ServiceStats
                    .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                    .Single().AttackPoints = serviceOffenseScore;
            }
            team.AttackPoints = offenseScore;
            team.TotalPoints += offenseScore;
            Logger.LogInformation($"Team {team.Name}: Offense={offenseScore}");
        }

        private static ServiceStatus ComputeServiceStatus(EnoEngineDBContext ctx, Team team, Service service, long roundId)
        {
            var currentRoundTasks = ctx.CheckerTasks
                .Where(ct => ct.RelatedRoundId == roundId)
                .Where(ct => ct.CurrentRoundId == roundId)
                .Where(ct => ct.TeamId == team.Id)
                .Where(ct => ct.ServiceId == service.Id)
                .AsNoTracking()
                .ToArray();
            foreach (var task in currentRoundTasks)
            {
                switch (task.CheckerResult)
                {
                    case CheckerResult.Ok:
                        continue;
                    case CheckerResult.Mumble:
                        return ServiceStatus.Mumble;
                    case CheckerResult.Down:
                        return ServiceStatus.Down;
                    default:
                        return ServiceStatus.CheckerError;
                }
            }

            // Current round was Ok, let's check the old ones
            var oldRoundTasks = ctx.CheckerTasks
                .Where(ct => ct.RelatedRoundId != roundId)
                .Where(ct => ct.CurrentRoundId == roundId)
                .Where(ct => ct.TeamId == team.Id)
                .Where(ct => ct.ServiceId == service.Id)
                .AsNoTracking()
                .ToArray();
            foreach (var task in oldRoundTasks)
            {
                switch (task.CheckerResult)
                {
                    case CheckerResult.Ok:
                        continue;
                    default:
                        return ServiceStatus.Recovering;
                }
            }
            
            return ServiceStatus.Ok;
        }

        public static async Task InsertCheckerLogMessage(CheckerLogMessage value)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                ctx.Logs.Add(value);
                await ctx.SaveChangesAsync();
            }
        }
    }
}
