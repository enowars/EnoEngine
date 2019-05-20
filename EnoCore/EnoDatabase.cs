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
using System.Runtime.InteropServices;

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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var databaseDomain = Environment.GetEnvironmentVariable("DATABASE_DOMAIN") ?? "localhost";
            optionsBuilder
                .UseNpgsql(
                    $@"Server={databaseDomain};Port=5432;Database=EnoDatabase;User Id=docker;Password=docker;Timeout=15;SslMode=Disable;",
                    options => options.EnableRetryOnFailure());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.Id);

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.CheckerTaskLaunchStatus);

            modelBuilder.Entity<CheckerTask>()
                .HasIndex(ct => ct.CurrentRoundId);

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

            modelBuilder.Entity<RoundTeamServiceState>()
                .HasIndex(rtss => rtss.GameRoundId);

            modelBuilder.Entity<SubmittedFlag>()
                .HasIndex(sf => sf.Id);

            modelBuilder.Entity<ServiceStats>()
                .HasIndex(ss => ss.Id);

            modelBuilder.Entity<SubmittedFlag>()
                .HasIndex(sf => new { sf.AttackerTeamId, sf.FlagId })
                .IsUnique();
        }
    }

    public class EnoDatabase
    {
        public static DBInitializationResult ApplyConfig(JsonConfiguration config, EnoLogger logger)
        {
            logger.LogTrace(new EnoLogMessage() {
                Message = "Applying configuration to database",
                Function = "ApplyConfig",
                Module = nameof(EnoDatabase)
            });
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

            Migrate(logger);
            using (var ctx = new EnoEngineDBContext())
            {
                var migrationResult = FillDatabase(ctx, config, logger);
                if (migrationResult.Success)
                    ctx.SaveChanges();
                return migrationResult;
            }
        }

        public static async Task<FlagSubmissionResult> InsertSubmittedFlag(string flag, string attackerAddressPrefix, JsonConfiguration config) //TODO more logs
        {

            while (true)
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
                            .Where(t => t.TeamSubnet == attackerAddressPrefix)
                            .AsNoTracking()
                            .SingleOrDefaultAsync();
                        var currentRound = await ctx.Rounds
                            .AsNoTracking()
                            .LastOrDefaultAsync();

                        if (dbFlag == null)
                            return FlagSubmissionResult.Invalid;
                        if (dbAttackerTeam == null)
                            return FlagSubmissionResult.InvalidSenderError;
                        if (dbFlag.Owner.Id == dbAttackerTeam.Id)
                            return FlagSubmissionResult.Own;
                        if (dbFlag.GameRoundId < currentRound.Id - config.FlagValidityInRounds)
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
                        ctx.SaveChanges();
                        return FlagSubmissionResult.Ok;
                    }
                }
                catch (DbUpdateException) {}
            }
        }

        public static void Migrate(EnoLogger logger)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                var pendingMigrations = ctx.Database.GetPendingMigrations().Count();
                if (pendingMigrations > 0)
                {
                    logger.LogInfo(new EnoLogMessage()
                    {
                        Message = $"Applying {pendingMigrations} migration(s)",
                        Function = "Migrate"
                    });
                    ctx.Database.Migrate();
                    ctx.SaveChanges();
                }
                else
                {
                    logger.LogDebug(new EnoLogMessage()
                    {
                        Message = $"No pending migrations",
                        Function = "Migrate"
                    });
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
                await RetryConnection(ctx);
                ctx.AddRange(tasks);
                await ctx.SaveChangesAsync();
            }
        }

        public static async Task<List<CheckerTask>> RetrievePendingCheckerTasks(int maxAmount)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                await RetryConnection(ctx);
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
                            StringRepresentation = EnoCoreUtils.GenerateSignedFlag((int) round.Id, (int) team.Id),
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
                    for (int i = 0; i < service.NoisesPerRound; i++)
                    {
                        var noise = new Noise()
                        {
                            Owner = team,
                            StringRepresentation = EnoCoreUtils.GenerateSignedNoise((int)round.Id, (int)team.Id),
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

        public static async Task InsertRetrieveOldFlagsTasks(Round currentRound, int oldRoundsCount, JsonConfiguration config)
        {
            int quarterRound = config.RoundLengthInSeconds / 4;
            using (var ctx = new EnoEngineDBContext())
            {
                await RetryConnection(ctx);
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
                        Address = $"service{oldFlag.ServiceId}.team{oldFlag.OwnerId}.{config.DnsSuffix}",
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
                        CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                        RoundLength = config.RoundLengthInSeconds
                    };
                    oldFlagsCheckerTasks.Add(task);
                    time = time.AddSeconds(timeDiff);
                }
                ctx.CheckerTasks.AddRange(oldFlagsCheckerTasks);
                await ctx.SaveChangesAsync();
            }
        }

        internal static EnoEngineScoreboard GetCurrentScoreboard(long roundId)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                var teams = ctx.Teams.AsNoTracking().OrderByDescending(t => t.TotalPoints).ToList();
                var round = ctx.Rounds
                    .AsNoTracking()
                    .Where(r => r.Id == roundId)
                    .LastOrDefault();
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

        private static DBInitializationResult FillDatabase(EnoEngineDBContext ctx, JsonConfiguration config, EnoLogger logger)
        {
            var staleDbTeamIds = ctx.Teams.Select(t => t.Id).ToList();

            // insert (valid!) teams
            foreach (var team in config.Teams)
            {
                if (team.Name == null)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = "Team name must not be null"
                    };

                if (team.TeamSubnet == null)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = "Team subnet must not be null"
                    };

                if (team.Id == 0)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = "Team must have a valid Id"
                    };

                string teamSubnet = EnoCoreUtils.ExtractSubnet(team.TeamSubnet, config.TeamSubnetBytesLength);

                // check if team is already present
                var dbTeam = ctx.Teams
                    .Where(t => t.Id == team.Id)
                    .SingleOrDefault();
                if (dbTeam == null)
                {
                    logger.LogInfo(new EnoLogMessage()
                    {
                        Message = $"Adding team {team.Name}({team.Id})",
                        Module = nameof(EnoDatabase),
                        TeamName = team.Name
                    });
                    ctx.Teams.Add(new Team()
                    {
                        TeamSubnet = teamSubnet,
                        Name = team.Name,
                        Id = team.Id
                    });
                }
                else
                {
                    dbTeam.TeamSubnet = teamSubnet;
                    dbTeam.Name = team.Name;
                    dbTeam.Id = team.Id;
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
            var staleDbServiceIds = ctx.Services.Select(t => t.Id).ToList();
            foreach (var service in config.Services)
            {
                if (service.Id == 0)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = "Service must have a valid Id"
                    };
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
                    .Where(s => s.Id == service.Id)
                    .SingleOrDefault();
                if (dbService == null)
                {
                    logger.LogInfo(new EnoLogMessage()
                    {
                        Message = $"Adding service {service.Name}",
                        Module = nameof(EnoDatabase),
                        ServiceName = service.Name
                    });
                    ctx.Services.Add(new Service()
                    {
                        Name = service.Name,
                        FlagsPerRound = service.FlagsPerRound,
                        NoisesPerRound = service.NoisesPerRound
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
                await RetryConnection(ctx);
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
                        var reportedStatus = await ComputeServiceStatus(ctx, team, service, roundId);
                        var roundTeamServiceState = new RoundTeamServiceState()
                        {
                            GameRoundId = round.Id,
                            ServiceId = service.Id,
                            TeamId = team.Id,
                            Status = reportedStatus
                        };
                        ctx.RoundTeamServiceStates.Add(roundTeamServiceState);

                        (await ctx.ServiceStats
                           .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                           .SingleAsync()).Status = reportedStatus;
                    }
                }
                await ctx.SaveChangesAsync();
            }
        }

        public static async Task CalculatedAllPoints(long roundId)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                int maxTasks = 16;
                //await RetryConnection(ctx); //Should not be necessary with limited Task pool.
                var services = ctx.Services.ToArray();
                var teams = await ctx.Teams
                    .AsNoTracking()
                    .ToArrayAsync();
                var tasks = new HashSet<Task>();
                foreach (var team in teams)
                {   
                    if(tasks.Count < maxTasks){ //If there are less Tasks then max allowed Task, spawn a new one.
                        tasks.Add(Task.Run(async () => await CalculateTeamScore(services, roundId, team)));
                    }
                    else{ //Wait for any Task to finish when max allowed Tasks are active.
                        Task finished = await Task.WhenAny(tasks);
                        tasks.Remove(finished);
                    }
                }
            }
        }

        public static async Task UpdateTaskCheckerTaskResults(Memory<CheckerTask> tasks)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                await RetryConnection(ctx);
                var tasksEnumerable = MemoryMarshal.ToEnumerable<CheckerTask>(tasks);
                ctx.UpdateRange(tasksEnumerable);
                await ctx.SaveChangesAsync();
            }
        }

        private static async Task CalculateTeamScore(Service[] services, long currentRoundId, Team team)
        {
            using (var ctx = new EnoEngineDBContext())
            {
                //await RetryConnection(ctx);
                team = await ctx.Teams.SingleAsync(t => t.Id == team.Id);
                team.TotalPoints = 0;
                await CalculateOffenseScore(ctx, services, currentRoundId, team);
                await CalculateDefenseScore(ctx, services, currentRoundId, team);
                await CalculateSLAScore(ctx, services, currentRoundId, team);
                await ctx.SaveChangesAsync();
            }
        }

        private static async Task CalculateSLAScore(EnoEngineDBContext ctx, Service[] services, long currentRoundId, Team team)
        {
            double slaScore = 0;
            double teamsCount = ctx.Teams.Count();
            foreach (var service in services)
            {
                double ups = await ctx.RoundTeamServiceStates
                    .Where(rtss => rtss.GameRoundId <= currentRoundId)
                    .Where(rtss => rtss.TeamId == team.Id)
                    .Where(rtss => rtss.ServiceId == service.Id)
                    .Where(rtss => rtss.Status == ServiceStatus.Ok)
                    .CountAsync();

                double recovers = await ctx.RoundTeamServiceStates
                    .Where(rtss => rtss.GameRoundId <= currentRoundId)
                    .Where(rtss => rtss.TeamId == team.Id)
                    .Where(rtss => rtss.ServiceId == service.Id)
                    .Where(rtss => rtss.Status == ServiceStatus.Recovering)
                    .CountAsync();

                double serviceSlaScore = (ups + 0.5 * recovers) * Math.Sqrt(teamsCount);
                slaScore += serviceSlaScore;
                (await ctx.ServiceStats
                    .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                    .SingleAsync()).ServiceLevelAgreementPoints = serviceSlaScore;
            }
            team.ServiceLevelAgreementPoints = slaScore;
            team.TotalPoints += slaScore;
        }

        private static async Task CalculateDefenseScore(EnoEngineDBContext ctx, Service[] services, long currentRoundId, Team team)
        {
            double teamDefenseScore = 0;
            foreach (var service in services)
            {
                double serviceDefenseScore = 0;
                var ownedFlags = ctx.Flags
                    .Where(f => f.OwnerId == team.Id)
                    .Where(f => f.ServiceId == service.Id)
                    .Where(f => f.GameRoundId <= currentRoundId);

                foreach (var ownedFlag in ownedFlags)
                {
                    double allCapturesOfFlag = (await ctx.SubmittedFlags
                        .Where(f => f.FlagId == ownedFlag.Id)
                        .CountAsync());

                    serviceDefenseScore -= Math.Pow(allCapturesOfFlag, 0.75);
                }
                teamDefenseScore += serviceDefenseScore;
                (await ctx.ServiceStats
                    .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                    .SingleAsync()).LostDefensePoints = serviceDefenseScore;
            }
            team.LostDefensePoints = teamDefenseScore;
            team.TotalPoints += teamDefenseScore;
        }

        private static async Task CalculateOffenseScore(EnoEngineDBContext ctx, Service[] services, long currentRoundId, Team team)
        {
            double offenseScore = 0;
            foreach (var service in services)
            {
                var flagsCapturedByTeam = ctx.SubmittedFlags
                    .Where(f => f.AttackerTeamId == team.Id)
                    .Where(f => f.RoundId <= currentRoundId)
                    .Include(f => f.Flag)
                    .Where(f => f.Flag.ServiceId == service.Id);

                double serviceOffenseScore = flagsCapturedByTeam.Count();
                foreach (var submittedFlag in flagsCapturedByTeam)
                {
                    double capturesOfFlag = await ctx.Flags
                        .Where(f => f.Id == submittedFlag.FlagId)
                        .CountAsync();
                    serviceOffenseScore += Math.Pow(1 / capturesOfFlag, 0.75);
                }

                offenseScore += serviceOffenseScore;
                (await ctx.ServiceStats
                    .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                    .SingleAsync()).AttackPoints = serviceOffenseScore;
            }
            team.AttackPoints = offenseScore;
            team.TotalPoints += offenseScore;
        }

        private static async Task<ServiceStatus> ComputeServiceStatus(EnoEngineDBContext ctx, Team team, Service service, long roundId)
        {
            var currentRoundTasks = await ctx.CheckerTasks
                .Where(ct => ct.CurrentRoundId == roundId)
                .Where(ct => ct.RelatedRoundId == roundId)
                .Where(ct => ct.TeamId == team.Id)
                .Where(ct => ct.ServiceId == service.Id)
                .AsNoTracking()
                .ToArrayAsync();
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

        private static async Task RetryConnection(EnoEngineDBContext ctx)
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    await ctx.Database.OpenConnectionAsync();
                    break;
                }
                catch (Exception)
                {
                    await Task.Delay(1);
                }
            }
        }
    }
}
