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
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Sockets;
using System.Data;
using EnoCore.Logging;
using EnoCore.Models;
using EnoCore.Configuration;
using EnoCore.Scoreboard;

namespace EnoDatabase
{
    public interface IEnoDatabase
    {
        void ApplyConfig(Configuration configuration);
        Task ProcessSubmissionsBatch(List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> result)> submissions, long flagValidityInRounds, EnoStatistics statistics);
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
        Task<(long newLatestSnapshotRoundId, long oldSnapshotRoundId, Service[] services, Team[] teams)> GetPointCalculationFrame(long roundId, Configuration configuration);
        Task CalculateTeamServicePoints(Team[] teams, long roundId, Service service, long oldSnapshotRoundId, long newLatestSnapshotRoundId);
        Task<Round> PrepareRecalculation();
    }

    public partial class EnoDatabase : IEnoDatabase
    {
        private readonly ILogger Logger;
        private readonly EnoDatabaseContext _context;

        public EnoDatabase(EnoDatabaseContext context, ILogger<EnoDatabase> logger)
        {
            _context = context;
            Logger = logger;
        }

        public void ApplyConfig(Configuration config)
        {
            Logger.LogDebug("Applying configuration to database");

            // Migrate if needed
            Migrate();

            // Get all teams from db
            var dbTeams = _context
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
                    Logger.LogInformation($"Adding team {team.Name}({team.Id})");
                    _context.Teams.Add(new Team()
                    {
                        TeamSubnet = team.TeamSubnet,
                        Name = team.Name,
                        Id = team.Id,
                        Active = team.Active,
                        Address = team.Address
                    });
                }
            }
            foreach (var (teamId, team) in dbTeams)
            {
                Logger.LogWarning($"Deactivating stale team in db ({teamId})");
                team.Active = false;
            }

            // Get all services from db
            var dbServices = _context
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
                    Logger.LogInformation($"Adding service {service.Name}");
                    _context.Services.Add(new Service(service.Id,
                        service.Name,
                        service.FlagsPerRound,
                        service.NoisesPerRound,
                        service.HavocsPerRound,
                        service.FlagStores,
                        service.Active));
                }
            }
            foreach (var (serviceId, service) in dbServices)
            {
                Logger.LogWarning($"Deactivating stale service in db ({serviceId})");
                service.Active = false;
            }

            _context.SaveChanges(); // Save so that the services and teams receive proper IDs
            foreach (var service in _context.Services.ToArray())
            {
                foreach (var team in _context.Teams.ToArray())
                {
                    var stats = _context.TeamServicePoints
                        .Where(ss => ss.TeamId == team.Id)
                        .Where(ss => ss.ServiceId == service.Id)
                        .SingleOrDefault();
                    if (stats == null)
                    {
                        _context.TeamServicePoints.Add(new(team.Id,
                            service.Id,
                            0,
                            0,
                            0,
                            ServiceStatus.OFFLINE,
                            null));
                    }
                }
            }
            _context.SaveChanges();
        }

        public void Migrate()
        {
            var pendingMigrations = _context.Database.GetPendingMigrations().Count();
            if (pendingMigrations > 0)
            {
                Logger.LogInformation($"Applying {pendingMigrations} migration(s)");
                _context.Database.Migrate();
                _context.SaveChanges();
                Logger.LogDebug($"Database migration complete");
            }
            else
            {
                Logger.LogDebug($"No pending migrations");
            }
        }

        public async Task<Team[]> RetrieveTeams()
        {
            return await _context.Teams
                .AsNoTracking()
                .ToArrayAsync();
        }

        public async Task<Service[]> RetrieveServices()
        {
            return await _context.Services
                .AsNoTracking()
                .ToArrayAsync();
        }

        public async Task<Round> CreateNewRound(DateTime begin, DateTime q2, DateTime q3, DateTime q4, DateTime end)
        {
            var oldRound = await _context.Rounds
                .OrderBy(r => r.Id)
                .LastOrDefaultAsync();
            long roundId;
            if (oldRound != null)
                roundId = oldRound.Id + 1;
            else
                roundId = 1;
            var round = new Round(roundId,
                begin,
                q2,
                q3,
                q4,
                end);
            _context.Rounds.Add(round);
            await _context.SaveChangesAsync();
            return round;
        }

        private async Task InsertCheckerTasks(IEnumerable<CheckerTask> tasks)
        {
            Logger.LogDebug($"InsertCheckerTasks inserting {tasks.Count()} tasks");
            _context.AddRange(tasks);
            await _context.SaveChangesAsync();
        }

        public async Task<List<CheckerTask>> RetrievePendingCheckerTasks(int maxAmount)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    var tasks = await _context.CheckerTasks
                        .Where(t => t.CheckerTaskLaunchStatus == CheckerTaskLaunchStatus.New)
                        .OrderBy(t => t.StartTime)
                        .Take(maxAmount)
                        .AsNoTracking()
                        .ToListAsync();

                    var launchedTasks = new CheckerTask[tasks.Count];
                    // TODO update launch status without delaying operation
                    for (int i = 0; i < launchedTasks.Length; i++)
                        launchedTasks[i] = tasks[i] with { CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Launched };

                    _context.UpdateRange(launchedTasks);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return tasks;
                }
                catch (Exception e)
                {
                    await transaction.RollbackAsync();
                    Logger.LogDebug($"RetrievePendingCheckerTasks: Rolling Back Transaction{e.ToFancyString()}");
                    throw new Exception(e.Message, e.InnerException);
                }
            });
        }
        public async Task<Round> GetLastRound()
        {
            var round = await _context.Rounds
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
                return;
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
                        var checkerTask = new CheckerTask(0,
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
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            CheckerTaskLaunchStatus.New);
                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await InsertCheckerTasks(tasks);
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
                return;
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
                        var checkerTask = new CheckerTask(0,
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
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            CheckerTaskLaunchStatus.New);
                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await InsertCheckerTasks(tasks);
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
                return;
            double timeDiff = (double)(maxRunningTime * 3) - 2 / tasksCount;
            var tasks = new CheckerTask[tasksCount];
            int i = 0;

            foreach (var service in config.ActiveServices)
            {
                var checkers = config.Checkers[service.Id];
                foreach (var team in config.ActiveTeams)
                {
                    for (int taskIndex = 0; taskIndex < service.HavocsPerRound; taskIndex++)
                    {
                        var checkerTask = new CheckerTask(0,
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
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            CheckerTaskLaunchStatus.New);
                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await InsertCheckerTasks(tasks);
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
                return;
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
                        var checkerTask = new CheckerTask(0,
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
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            CheckerTaskLaunchStatus.New);
                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await InsertCheckerTasks(tasks);
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
                return;
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
                            var task = new CheckerTask(0,
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
                                CheckerResult.INTERNAL_ERROR,
                                null,
                                CheckerTaskLaunchStatus.New);
                            tasks[i] = task;
                            taskStart = taskStart.AddSeconds(timeDiff);
                            i += 1;
                        }
                    }
                }
            }

            //TODO shuffle
            await InsertCheckerTasks(tasks);
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
                return;
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
                        var checkerTask = new CheckerTask(0,
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
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            CheckerTaskLaunchStatus.New);

                        tasks[i] = checkerTask;
                        taskStart = taskStart.AddSeconds(timeDiff);
                        i += 1;
                    }
                }
            }

            // TODO shuffle
            await InsertCheckerTasks(tasks);
        }

        public async Task CalculateRoundTeamServiceStates(IServiceProvider serviceProvider, long roundId, EnoStatistics statistics)
        {
            var teams = await _context.Teams.AsNoTracking().ToArrayAsync();
            var services = await _context.Services.AsNoTracking().ToArrayAsync();

            //Logger.LogError("Before Statement");
            var currentRoundWorstResults = new Dictionary<(long ServiceId, long TeamId), CheckerTask?>();
            var sw = new Stopwatch();
            sw.Start();
            var foo = await _context.CheckerTasks
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
            Logger.LogInformation($"CalculateRoundTeamServiceStates: Data Aggregation for {teams.Length} Teams and {services.Length} Services took {sw.ElapsedMilliseconds}ms");

            var oldRoundsWorstResults = await _context.CheckerTasks
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
                    newRoundTeamServiceStatus[(key.ServiceId, key.TeamId)] = new RoundTeamServiceStatus(status,
                        message,
                        key.TeamId,
                        key.ServiceId,
                        roundId);
                }
            }
            _context.RoundTeamServiceStatus.AddRange(newRoundTeamServiceStatus.Values);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateTaskCheckerTaskResults(Memory<CheckerTask> tasks)
        {
            var tasksEnumerable = MemoryMarshal.ToEnumerable<CheckerTask>(tasks);
            _context.UpdateRange(tasksEnumerable);
            await _context.SaveChangesAsync();
        }

        public async Task<Team?> GetTeamIdByPrefix(byte[] attackerPrefixString)
        {
            return await _context.Teams
                .Where(t => t.TeamSubnet == attackerPrefixString)
                .SingleOrDefaultAsync();
        }

        public async Task<Round> PrepareRecalculation()
        {
            await _context.Database.ExecuteSqlRawAsync($"delete from \"{nameof(_context.TeamServicePointsSnapshot)}\";");
            return await _context.Rounds
                .OrderByDescending(r => r.Id)
                .Skip(1)
                .FirstOrDefaultAsync();
        }
    }
}
