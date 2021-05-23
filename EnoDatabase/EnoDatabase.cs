namespace EnoDatabase
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Configuration;
    using EnoCore.Logging;
    using EnoCore.Models;
    using EnoCore.Scoreboard;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public interface IEnoDatabase
    {
#pragma warning disable SA1516 // Elements should be separated by blank line
        void ApplyConfig(Configuration configuration);
        Task ProcessSubmissionsBatch(List<(Flag Flag, long AttackerTeamId, TaskCompletionSource<FlagSubmissionResult> Result)> submissions, long flagValidityInRounds, EnoStatistics statistics);
        Task<Team[]> RetrieveTeams();
        Task<Service[]> RetrieveServices();
        Task<Round> CreateNewRound(DateTime begin, DateTime q2, DateTime q3, DateTime q4, DateTime end);
        Task CalculateRoundTeamServiceStates(IServiceProvider serviceProvider, long roundId, EnoStatistics statistics);
        Task InsertPutFlagsTasks(Round round, Configuration config);
        Task InsertPutNoisesTasks(Round currentRound, Configuration config);
        Task InsertHavocsTasks(Round currentRound, Configuration config);
        Task InsertRetrieveCurrentFlagsTasks(Round round, Configuration config);
        Task InsertRetrieveOldFlagsTasks(Round currentRound, Configuration config);
        Task<Team?> GetTeamIdByPrefix(byte[] attackerPrefixString);
        Task InsertRetrieveCurrentNoisesTasks(Round currentRound, Configuration config);
        Task<List<CheckerTask>> RetrievePendingCheckerTasks(int maxAmount);
        Task CalculateTotalPoints();
        Task<Round> GetLastRound();
        Task<Scoreboard> GetCurrentScoreboard(long roundId);
        void Migrate();
        Task UpdateTaskCheckerTaskResults(Memory<CheckerTask> tasks);
        Task<(long NewLatestSnapshotRoundId, long OldSnapshotRoundId, Service[] Services, Team[] Teams)> GetPointCalculationFrame(long roundId, Configuration configuration);
        Task CalculateTeamServicePoints(Team[] teams, long roundId, Service service, long oldSnapshotRoundId, long newLatestSnapshotRoundId);
        Task<Round> PrepareRecalculation();
#pragma warning restore SA1516 // Elements should be separated by blank line
    }

    public partial class EnoDatabase : IEnoDatabase
    {
        private readonly ILogger logger;
        private readonly EnoDatabaseContext context;

        public EnoDatabase(EnoDatabaseContext context, ILogger<EnoDatabase> logger)
        {
            this.context = context;
            this.logger = logger;
        }

        public void ApplyConfig(Configuration config)
        {
            this.logger.LogDebug("Applying configuration to database");

            // Migrate if needed
            this.Migrate();

            // Get all teams from db
            var dbTeams = this.context
                .Teams
                .ToList()
                .ToDictionary(t => t.Id);

            // Insert or update teams from config
            foreach (var team in config.Teams)
            {
                if (dbTeams.TryGetValue(team.Id, out var dbTeam))
                {
                    dbTeam.TeamSubnet = team.TeamSubnet;
                    dbTeam.Name = team.Name;
                    dbTeam.Id = team.Id;
                    dbTeam.Active = team.Active;
                    dbTeam.Address = team.Address;
                    dbTeams.Remove(team.Id);
                }
                else
                {
                    this.logger.LogInformation($"Adding team {team.Name}({team.Id})");
                    this.context.Teams.Add(new Team()
                    {
                        TeamSubnet = team.TeamSubnet,
                        Name = team.Name,
                        LogoUrl = team.LogoUrl,
                        CountryCode = team.CountryFlagUrl,
                        Id = team.Id,
                        Active = team.Active,
                        Address = team.Address,
                    });
                }
            }

            foreach (var (teamId, team) in dbTeams)
            {
                this.logger.LogWarning($"Deactivating stale team in db ({teamId})");
                team.Active = false;
            }

            // Get all services from db
            var dbServices = this.context
                .Services
                .ToList()
                .ToDictionary(s => s.Id);

            // Insert or update services from config
            foreach (var service in config.Services)
            {
                if (dbServices.TryGetValue(service.Id, out var dbService))
                {
                    dbService.Name = service.Name;
                    dbService.FlagsPerRound = service.FlagsPerRound;
                    dbService.NoisesPerRound = service.NoisesPerRound;
                    dbService.HavocsPerRound = service.HavocsPerRound;
                    dbService.Active = service.Active;
                    dbServices.Remove(dbService.Id);
                }
                else
                {
                    var newService = new Service(
                        service.Id,
                        service.Name,
                        service.FlagsPerRound,
                        service.NoisesPerRound,
                        service.HavocsPerRound,
                        service.FlagVariants,
                        service.NoiseVariants,
                        service.HavocVariants,
                        service.Active);
                    this.logger.LogInformation($"Adding service {newService}");
                    this.context.Services.Add(newService);
                }
            }

            foreach (var (serviceId, service) in dbServices)
            {
                this.logger.LogWarning($"Deactivating stale service in db ({serviceId})");
                service.Active = false;
            }

            this.context.SaveChanges(); // Save so that the services and teams receive proper IDs

            foreach (var service in this.context.Services.ToArray())
            {
                foreach (var team in this.context.Teams.ToArray())
                {
                    var stats = this.context.TeamServicePoints
                        .Where(ss => ss.TeamId == team.Id)
                        .Where(ss => ss.ServiceId == service.Id)
                        .SingleOrDefault();
                    if (stats == null)
                    {
                        this.context.TeamServicePoints.Add(new(
                            team.Id,
                            service.Id,
                            0,
                            0,
                            0,
                            ServiceStatus.OFFLINE,
                            null));
                    }
                }
            }

            this.context.SaveChanges();
        }

        public void Migrate()
        {
            var pendingMigrations = this.context.Database.GetPendingMigrations().Count();
            if (pendingMigrations > 0)
            {
                this.logger.LogInformation($"Applying {pendingMigrations} migration(s)");
                this.context.Database.Migrate();
                this.context.SaveChanges();
                this.logger.LogDebug($"Database migration complete");
            }
            else
            {
                this.logger.LogDebug($"No pending migrations");
            }
        }

        public async Task<Team[]> RetrieveTeams()
        {
            return await this.context.Teams
                .AsNoTracking()
                .ToArrayAsync();
        }

        public async Task<Service[]> RetrieveServices()
        {
            return await this.context.Services
                .AsNoTracking()
                .ToArrayAsync();
        }

        public async Task<Round> CreateNewRound(DateTime begin, DateTime q2, DateTime q3, DateTime q4, DateTime end)
        {
            var oldRound = await this.context.Rounds
                .OrderBy(r => r.Id)
                .LastOrDefaultAsync();
            long roundId;
            if (oldRound != null)
            {
                roundId = oldRound.Id + 1;
            }
            else
            {
                roundId = 1;
            }

            var round = new Round(
                roundId,
                begin,
                q2,
                q3,
                q4,
                end);
            this.context.Rounds.Add(round);
            await this.context.SaveChangesAsync();
            return round;
        }

        public async Task<List<CheckerTask>> RetrievePendingCheckerTasks(int maxAmount)
        {
            var strategy = this.context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = this.context.Database.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    var tasks = await this.context.CheckerTasks
                        .Where(t => t.CheckerTaskLaunchStatus == CheckerTaskLaunchStatus.New)
                        .OrderBy(t => t.StartTime)
                        .Take(maxAmount)
                        .AsNoTracking()
                        .ToListAsync();

                    var launchedTasks = new CheckerTask[tasks.Count];

                    // TODO update launch status without delaying operation
                    for (int i = 0; i < launchedTasks.Length; i++)
                    {
                        launchedTasks[i] = tasks[i] with { CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Launched };
                    }

                    this.context.UpdateRange(launchedTasks);
                    await this.context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return tasks;
                }
                catch (Exception e)
                {
                    await transaction.RollbackAsync();
                    this.logger.LogDebug($"RetrievePendingCheckerTasks: Rolling Back Transaction{e.ToFancyStringWithCaller()}");
                    throw new Exception(e.Message, e.InnerException);
                }
            });
        }

        public async Task<Round> GetLastRound()
        {
            var round = await this.context.Rounds
                .OrderByDescending(f => f.Id)
                .FirstOrDefaultAsync();
            return round;
        }

        public async Task InsertPutFlagsTasks(Round round, Configuration config)
        {
            // putflags are started in Q1
            double maxRunningTime = config.RoundLengthInSeconds / 4;
            var taskStart = round.Begin;
            int tasksCount = 0;
            foreach (var service in config.ActiveServices)
            {
                tasksCount += service.FlagsPerRound * config.ActiveTeams.Count;
            }

            if (tasksCount == 0)
            {
                return;
            }

            double timeDiff = (maxRunningTime - 2) / tasksCount;
            var tasks = new CheckerTask[tasksCount];
            int i = 0;

            foreach (var service in config.ActiveServices)
            {
                var checkers = config.Checkers[service.Id];
                foreach (var team in config.ActiveTeams)
                {
                    for (int taskIndex = 0; taskIndex < service.FlagsPerRound; taskIndex++)
                    {
                        var checkerTask = new CheckerTask(
                            0,
                            checkers[i % checkers.Length],
                            CheckerTaskMethod.putflag,
                            team.Address ?? $"team{team.Id}.{config.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            new Flag(team.Id, service.Id, taskIndex, round.Id, 0).ToString(Encoding.ASCII.GetBytes(config.FlagSigningKey), config.Encoding),
                            taskStart,
                            (int)(maxRunningTime * 1000),
                            config.RoundLengthInSeconds,
                            taskIndex,
                            taskIndex % service.FlagVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);
                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertPutNoisesTasks(Round round, Configuration config)
        {
            // putnoises are started in Q1
            double maxRunningTime = config.RoundLengthInSeconds / 4;
            var taskStart = round.Begin;
            int tasksCount = 0;
            foreach (var service in config.ActiveServices)
            {
                tasksCount += service.NoisesPerRound * config.ActiveTeams.Count;
            }

            if (tasksCount == 0)
            {
                return;
            }

            double timeDiff = (maxRunningTime - 2) / tasksCount;
            var tasks = new CheckerTask[tasksCount];
            int i = 0;

            foreach (var service in config.ActiveServices)
            {
                var checkers = config.Checkers[service.Id];
                foreach (var team in config.ActiveTeams)
                {
                    for (int taskIndex = 0; taskIndex < service.NoisesPerRound; taskIndex++)
                    {
                        var checkerTask = new CheckerTask(
                            0,
                            checkers[i % checkers.Length],
                            CheckerTaskMethod.putnoise,
                            team.Address ?? $"team{team.Id}.{config.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            null,
                            taskStart,
                            (int)(maxRunningTime * 1000),
                            config.RoundLengthInSeconds,
                            taskIndex,
                            taskIndex % service.NoiseVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);
                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertHavocsTasks(Round round, Configuration config)
        {
            // havocs are started in Q1, Q2 and Q3
            double maxRunningTime = config.RoundLengthInSeconds / 4;
            var taskStart = round.Begin;
            int tasksCount = 0;
            foreach (var service in config.ActiveServices)
            {
                tasksCount += service.HavocsPerRound * config.ActiveTeams.Count;
            }

            if (tasksCount == 0)
            {
                return;
            }

            double timeDiff = (double)((maxRunningTime * 3) - 2) / tasksCount;
            var tasks = new CheckerTask[tasksCount];
            int i = 0;

            foreach (var service in config.ActiveServices)
            {
                var checkers = config.Checkers[service.Id];
                foreach (var team in config.ActiveTeams)
                {
                    for (int taskIndex = 0; taskIndex < service.HavocsPerRound; taskIndex++)
                    {
                        var checkerTask = new CheckerTask(
                            0,
                            checkers[i % checkers.Length],
                            CheckerTaskMethod.havoc,
                            team.Address ?? $"team{team.Id}.{config.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            null,
                            taskStart,
                            (int)(maxRunningTime * 1000),
                            config.RoundLengthInSeconds,
                            taskIndex,
                            taskIndex % service.HavocVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);
                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertRetrieveCurrentFlagsTasks(Round round, Configuration config)
        {
            // getflags for new flags are started in Q3
            double maxRunningTime = config.RoundLengthInSeconds / 4;
            var taskStart = round.Quarter3;
            int tasksCount = 0;
            foreach (var service in config.ActiveServices)
            {
                tasksCount += service.FlagsPerRound * config.ActiveTeams.Count;
            }

            if (tasksCount == 0)
            {
                return;
            }

            double timeDiff = (maxRunningTime - 2) / tasksCount;
            var tasks = new CheckerTask[tasksCount];
            int i = 0;

            foreach (var service in config.ActiveServices)
            {
                var checkers = config.Checkers[service.Id];
                foreach (var team in config.ActiveTeams)
                {
                    for (int taskIndex = 0; taskIndex < service.FlagsPerRound; taskIndex++)
                    {
                        var checkerTask = new CheckerTask(
                            0,
                            checkers[i % checkers.Length],
                            CheckerTaskMethod.getflag,
                            team.Address ?? $"team{team.Id}.{config.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            new Flag(team.Id, service.Id, taskIndex, round.Id, 0).ToString(Encoding.ASCII.GetBytes(config.FlagSigningKey), config.Encoding),
                            taskStart,
                            (int)(maxRunningTime * 1000),
                            config.RoundLengthInSeconds,
                            taskIndex,
                            taskIndex % service.FlagVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);
                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertRetrieveOldFlagsTasks(Round round, Configuration config)
        {
            // getflags for old flags are started in Q2
            double maxRunningTime = config.RoundLengthInSeconds / 4;
            var taskStart = round.Quarter2;
            int tasksCount = 0;
            int oldRoundsCount = (int)Math.Min(config.CheckedRoundsPerRound, round.Id) - 1;
            foreach (var service in config.ActiveServices)
            {
                tasksCount += service.FlagsPerRound
                    * config.ActiveTeams.Count
                    * oldRoundsCount;
            }

            if (tasksCount == 0)
            {
                return;
            }

            double timeDiff = (maxRunningTime - 2) / tasksCount;
            var tasks = new CheckerTask[tasksCount];
            int i = 0;

            for (long oldRoundId = round.Id - 1; oldRoundId > (round.Id - config.CheckedRoundsPerRound) && oldRoundId > 0; oldRoundId--)
            {
                foreach (var service in config.ActiveServices)
                {
                    var checkers = config.Checkers[service.Id];
                    foreach (var team in config.ActiveTeams)
                    {
                        for (int taskIndex = 0; taskIndex < service.FlagsPerRound; taskIndex++)
                        {
                            var task = new CheckerTask(
                                0,
                                checkers[i % checkers.Length],
                                CheckerTaskMethod.getflag,
                                team.Address ?? $"team{team.Id}.{config.DnsSuffix}",
                                service.Id,
                                service.Name,
                                team.Id,
                                team.Name,
                                oldRoundId,
                                round.Id,
                                new Flag(team.Id, service.Id, taskIndex, oldRoundId, 0).ToString(Encoding.ASCII.GetBytes(config.FlagSigningKey), config.Encoding),
                                taskStart,
                                (int)(maxRunningTime * 1000),
                                config.RoundLengthInSeconds,
                                taskIndex,
                                taskIndex % service.FlagVariants,
                                CheckerResult.INTERNAL_ERROR,
                                null,
                                null,
                                CheckerTaskLaunchStatus.New);
                            tasks[i] = task;
                            taskStart = taskStart.AddSeconds(timeDiff);
                            i += 1;
                        }
                    }
                }
            }

            // TODO shuffle
            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertRetrieveCurrentNoisesTasks(Round round, Configuration config)
        {
            // getnoises are started in Q1
            double maxRunningTime = config.RoundLengthInSeconds / 4;
            var taskStart = round.Quarter3;
            int tasksCount = 0;
            foreach (var service in config.ActiveServices)
            {
                tasksCount += service.NoisesPerRound * config.ActiveTeams.Count;
            }

            if (tasksCount == 0)
            {
                return;
            }

            double timeDiff = (maxRunningTime - 2) / tasksCount;
            var tasks = new CheckerTask[tasksCount];
            int i = 0;

            foreach (var service in config.ActiveServices)
            {
                var checkers = config.Checkers[service.Id];
                foreach (var team in config.ActiveTeams)
                {
                    for (int taskIndex = 0; taskIndex < service.NoisesPerRound; taskIndex++)
                    {
                        var checkerTask = new CheckerTask(
                            0,
                            checkers[i % checkers.Length],
                            CheckerTaskMethod.getnoise,
                            team.Address ?? $"team{team.Id}.{config.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            null,
                            taskStart,
                            (int)(maxRunningTime * 1000),
                            config.RoundLengthInSeconds,
                            taskIndex,
                            taskIndex % service.NoiseVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);

                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await this.InsertCheckerTasks(tasks);
        }

        public async Task CalculateRoundTeamServiceStates(IServiceProvider serviceProvider, long roundId, EnoStatistics statistics)
        {
            var teams = await this.context.Teams.AsNoTracking().ToArrayAsync();
            var services = await this.context.Services.AsNoTracking().ToArrayAsync();

            var currentRoundWorstResults = new Dictionary<(long ServiceId, long TeamId), CheckerTask?>();
            var sw = new Stopwatch();
            sw.Start();
            var foo = await this.context.CheckerTasks
                            .TagWith("CalculateRoundTeamServiceStates:currentRoundTasks")
                            .Where(ct => ct.CurrentRoundId == roundId)
                            .Where(ct => ct.RelatedRoundId == roundId)
                            .OrderBy(ct => ct.CheckerResult)
                            .ThenBy(ct => ct.StartTime)
                            .ToListAsync();
            foreach (var e in foo)
            {
                if (!currentRoundWorstResults.ContainsKey((e.ServiceId, e.TeamId)))
                {
                    currentRoundWorstResults[(e.ServiceId, e.TeamId)] = e;
                }
            }

            sw.Stop();
            statistics.LogCheckerTaskAggregateMessage(roundId, sw.ElapsedMilliseconds);
            this.logger.LogInformation($"CalculateRoundTeamServiceStates: Data Aggregation for {teams.Length} Teams and {services.Length} Services took {sw.ElapsedMilliseconds}ms");

            var oldRoundsWorstResults = await this.context.CheckerTasks
                .TagWith("CalculateRoundTeamServiceStates:oldRoundsTasks")
                .Where(ct => ct.CurrentRoundId == roundId)
                .Where(ct => ct.RelatedRoundId != roundId)
                .GroupBy(ct => new { ct.ServiceId, ct.TeamId })
                .Select(g => new { g.Key, WorstResult = g.Min(ct => ct.CheckerResult) })
                .AsNoTracking()
                .ToDictionaryAsync(g => g.Key, g => g.WorstResult);

            var newRoundTeamServiceStatus = new Dictionary<(long ServiceId, long TeamId), RoundTeamServiceStatus>();
            foreach (var team in teams)
            {
                foreach (var service in services)
                {
                    var key2 = (service.Id, team.Id);
                    var key = new { ServiceId = service.Id, TeamId = team.Id };
                    ServiceStatus status = ServiceStatus.INTERNAL_ERROR;
                    string? message = null;
                    if (currentRoundWorstResults.ContainsKey(key2))
                    {
                        if (currentRoundWorstResults[key2] != null)
                        {
                            status = currentRoundWorstResults[key2]!.CheckerResult.AsServiceStatus();
                            message = currentRoundWorstResults[key2]!.ErrorMessage;
                        }
                        else
                        {
                            status = ServiceStatus.OK;
                            message = null;
                        }
                    }

                    if (status == ServiceStatus.OK && oldRoundsWorstResults.ContainsKey(key))
                    {
                        if (oldRoundsWorstResults[key] != CheckerResult.OK)
                        {
                            status = ServiceStatus.RECOVERING;
                        }
                    }

                    newRoundTeamServiceStatus[(key.ServiceId, key.TeamId)] = new RoundTeamServiceStatus(
                        status,
                        message,
                        key.TeamId,
                        key.ServiceId,
                        roundId);
                }
            }

            this.context.RoundTeamServiceStatus.AddRange(newRoundTeamServiceStatus.Values);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateTaskCheckerTaskResults(Memory<CheckerTask> tasks)
        {
            var tasksEnumerable = MemoryMarshal.ToEnumerable<CheckerTask>(tasks);
            this.context.UpdateRange(tasksEnumerable);
            await this.context.SaveChangesAsync();
        }

        public async Task<Team?> GetTeamIdByPrefix(byte[] attackerPrefixString)
        {
            return await this.context.Teams
                .Where(t => t.TeamSubnet == attackerPrefixString)
                .SingleOrDefaultAsync();
        }

        public async Task<Round> PrepareRecalculation()
        {
            await this.context.Database.ExecuteSqlRawAsync($"delete from \"{nameof(this.context.TeamServicePointsSnapshot)}\";");
            return await this.context.Rounds
                .OrderByDescending(r => r.Id)
                .Skip(1)
                .FirstOrDefaultAsync();
        }

        private async Task InsertCheckerTasks(IEnumerable<CheckerTask> tasks)
        {
            this.logger.LogDebug($"InsertCheckerTasks inserting {tasks.Count()} tasks");
            this.context.AddRange(tasks);
            await this.context.SaveChangesAsync();
        }
    }
}
