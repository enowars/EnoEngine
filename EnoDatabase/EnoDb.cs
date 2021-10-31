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
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using EEnoCore.Models.AttackInfo;
    using EnoCore;
    using EnoCore.Logging;
    using EnoCore.Models;
    using EnoCore.Models.Database;
    using EnoCore.Models.Scoreboard;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public partial class EnoDb
    {
        private readonly ILogger logger;
        private readonly EnoDbContext context;

        public EnoDb(EnoDbContext context, ILogger<EnoDb> logger)
        {
            this.context = context;
            this.logger = logger;
        }

        public async Task<Team[]> RetrieveActiveTeams()
        {
            return await this.context.Teams
                .Where(t => t.Active)
                .AsNoTracking()
                .ToArrayAsync();
        }

        public async Task<Service[]> RetrieveActiveServices()
        {
            return await this.context.Services
                .Where(t => t.Active)
                .AsNoTracking()
                .ToArrayAsync();
        }

        public async Task<Configuration> RetrieveConfiguration()
        {
            return await this.context.Configurations
                .SingleOrDefaultAsync();
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

        public async Task InsertPutFlagsTasks(
            Round round,
            Team[] activeTeams,
            Service[] activeServices,
            Configuration configuration)
        {
            // putflags are started in Q1
            double quarterLength = configuration.RoundLengthInSeconds / 4;
            int tasksCount = 0;
            foreach (var service in activeServices)
            {
                tasksCount += (int)service.FlagsPerRound * activeTeams.Length;
            }

            if (tasksCount == 0)
            {
                // TODO warn if not test mode
                return;
            }

            var tasks = new List<CheckerTask>(tasksCount);
            var taskStart = round.Begin;
            double teamStepLength = (quarterLength - 2) / activeTeams.Length;
            double serviceStepLength = teamStepLength / activeServices.Length;
            foreach (var service in activeServices)
            {
                double serviceOffset = (service.Id - 1) * serviceStepLength;
                for (int variantIndex = 0; variantIndex < service.FlagsPerRound; variantIndex++)
                {
                    double variantOffset = variantIndex * (serviceStepLength / service.FlagsPerRound);
                    int i = 0;
                    var currentTasks = new List<CheckerTask>();
                    foreach (var team in activeTeams)
                    {
                        double taskOffset = (i * teamStepLength) + serviceOffset + variantOffset;

                        // this.logger.LogDebug($"InsertPutFlagsTasks taskOffset={taskOffset}");
                        var checkerTask = new CheckerTask(
                            0,
                            service.Checkers[i % service.Checkers.Length],
                            CheckerTaskMethod.putflag,
                            team.Address ?? $"team{team.Id}.{configuration.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            new Flag(team.Id, service.Id, variantIndex, round.Id, 0).ToString(configuration.FlagSigningKey, configuration.Encoding),
                            taskStart.AddSeconds(taskOffset),
                            (int)(quarterLength * 1000),
                            configuration.RoundLengthInSeconds,
                            variantIndex,
                            variantIndex % service.FlagVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);
                        currentTasks.Add(checkerTask);
                        i += 1;
                    }

                    this.FisherYatesShuffleTaskStarts(currentTasks);
                    tasks.AddRange(currentTasks);
                }
            }

            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertPutNoisesTasks(
            Round round,
            Team[] activeTeams,
            Service[] activeServices,
            Configuration configuration)
        {
            // putnoises are started in Q1
            double quarterLength = configuration.RoundLengthInSeconds / 4;
            int tasksCount = 0;
            foreach (var service in activeServices)
            {
                tasksCount += (int)service.NoisesPerRound * activeTeams.Length;
            }

            if (tasksCount == 0)
            {
                return;
            }

            var tasks = new List<CheckerTask>(tasksCount);
            var taskStart = round.Begin;
            double teamStepLength = (quarterLength - 2) / activeTeams.Length;
            double serviceStepLength = teamStepLength / activeServices.Length;
            foreach (var service in activeServices)
            {
                double serviceOffset = (service.Id - 1) * serviceStepLength;
                for (int variantIndex = 0; variantIndex < service.NoisesPerRound; variantIndex++)
                {
                    double variantOffset = variantIndex * (serviceStepLength / service.NoisesPerRound);
                    int i = 0;
                    var currentTasks = new List<CheckerTask>();
                    foreach (var team in activeTeams)
                    {
                        double taskOffset = (i * teamStepLength) + serviceOffset + variantOffset;

                        // this.logger.LogDebug($"InsertPutNoisesTasks taskOffset={taskOffset}");
                        var checkerTask = new CheckerTask(
                            0,
                            service.Checkers[i % service.Checkers.Length],
                            CheckerTaskMethod.putnoise,
                            team.Address ?? $"team{team.Id}.{configuration.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            null,
                            taskStart.AddSeconds(taskOffset),
                            (int)(quarterLength * 1000),
                            configuration.RoundLengthInSeconds,
                            variantIndex,
                            variantIndex % service.NoiseVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);
                        currentTasks.Add(checkerTask);
                        i += 1;
                    }

                    this.FisherYatesShuffleTaskStarts(currentTasks);
                    tasks.AddRange(currentTasks);
                }
            }

            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertHavocsTasks(
            Round round,
            Team[] activeTeams,
            Service[] activeServices,
            Configuration configuration)
        {
            // havocs are started in Q1, Q2 and Q3
            double quarterLength = configuration.RoundLengthInSeconds / 4;
            int tasksCount = 0;
            foreach (var service in activeServices)
            {
                tasksCount += (int)service.HavocsPerRound * activeTeams.Length;
            }

            if (tasksCount == 0)
            {
                return;
            }

            var tasks = new List<CheckerTask>(tasksCount);
            var taskStart = round.Begin;
            double teamStepLength = ((3 * quarterLength) - 2) / activeTeams.Length;
            double serviceStepLength = teamStepLength / activeServices.Length;
            foreach (var service in activeServices)
            {
                double serviceOffset = (service.Id - 1) * serviceStepLength;
                for (int variantIndex = 0; variantIndex < service.HavocsPerRound; variantIndex++)
                {
                    double variantOffset = variantIndex * (serviceStepLength / service.HavocsPerRound);
                    int i = 0;
                    var currentTasks = new List<CheckerTask>();
                    foreach (var team in activeTeams)
                    {
                        double taskOffset = (i * teamStepLength) + serviceOffset + variantOffset;

                        // this.logger.LogDebug($"InsertHavocsTasks taskOffset={taskOffset}");
                        var checkerTask = new CheckerTask(
                            0,
                            service.Checkers[i % service.Checkers.Length],
                            CheckerTaskMethod.havoc,
                            team.Address ?? $"team{team.Id}.{configuration.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            null,
                            taskStart.AddSeconds(taskOffset),
                            (int)(quarterLength * 1000),
                            configuration.RoundLengthInSeconds,
                            variantIndex,
                            variantIndex % service.HavocVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);
                        currentTasks.Add(checkerTask);
                        i += 1;
                    }

                    this.FisherYatesShuffleTaskStarts(currentTasks);
                    tasks.AddRange(currentTasks);
                }
            }

            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertRetrieveCurrentFlagsTasks(
            Round round,
            Team[] activeTeams,
            Service[] activeServices,
            Configuration configuration)
        {
            // getflags for new flags are started in Q3
            double quarterLength = configuration.RoundLengthInSeconds / 4;
            int tasksCount = 0;
            foreach (var service in activeServices)
            {
                tasksCount += (int)service.FlagsPerRound * activeTeams.Length;
            }

            if (tasksCount == 0)
            {
                return;
            }

            var tasks = new List<CheckerTask>(tasksCount);
            var taskStart = round.Quarter3;
            double teamStepLength = (quarterLength - 2) / activeTeams.Length;
            double serviceStepLength = teamStepLength / activeServices.Length;
            foreach (var service in activeServices)
            {
                double serviceOffset = (service.Id - 1) * serviceStepLength;
                for (int variantIndex = 0; variantIndex < service.FlagsPerRound; variantIndex++)
                {
                    double variantOffset = variantIndex * (serviceStepLength / service.FlagsPerRound);
                    int i = 0;
                    var currentTasks = new List<CheckerTask>();
                    foreach (var team in activeTeams)
                    {
                        double taskOffset = (i * teamStepLength) + serviceOffset + variantOffset;

                        // this.logger.LogDebug($"InsertRetrieveCurrentFlagsTasks Q3 taskOffset={taskOffset}");
                        var checkerTask = new CheckerTask(
                            0,
                            service.Checkers[i % service.Checkers.Length],
                            CheckerTaskMethod.getflag,
                            team.Address ?? $"team{team.Id}.{configuration.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            new Flag(team.Id, service.Id, variantIndex, round.Id, 0).ToString(configuration.FlagSigningKey, configuration.Encoding),
                            taskStart.AddSeconds(taskOffset),
                            (int)(quarterLength * 1000),
                            configuration.RoundLengthInSeconds,
                            variantIndex,
                            variantIndex % service.FlagVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);
                        currentTasks.Add(checkerTask);
                        i += 1;
                    }

                    this.FisherYatesShuffleTaskStarts(currentTasks);
                    tasks.AddRange(currentTasks);
                }
            }

            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertRetrieveOldFlagsTasks(
            Round round,
            Team[] activeTeams,
            Service[] activeServices,
            Configuration configuration)
        {
            // getflags for old flags are started in Q2 and Q3
            double quarterLength = configuration.RoundLengthInSeconds / 4;
            int tasksCount = 0;
            int oldRoundsCount = (int)Math.Min(configuration.CheckedRoundsPerRound, round.Id) - 1;
            foreach (var service in activeServices)
            {
                tasksCount += (int)service.FlagsPerRound
                    * activeTeams.Length
                    * oldRoundsCount;
            }

            if (tasksCount == 0)
            {
                return;
            }

            var tasks = new List<CheckerTask>(tasksCount);
            var taskStart = round.Quarter2;

            for (long oldRoundId = round.Id - 1; oldRoundId > (round.Id - configuration.CheckedRoundsPerRound) && oldRoundId > 0; oldRoundId--)
            {
                double teamStepLength = ((2 * quarterLength) - 2) / activeTeams.Length;
                double serviceStepLength = teamStepLength / activeServices.Length;
                foreach (var service in activeServices)
                {
                    double serviceOffset = (service.Id - 1) * serviceStepLength;
                    for (int variantIndex = 0; variantIndex < service.FlagsPerRound; variantIndex++)
                    {
                        double variantOffset = variantIndex * (serviceStepLength / service.FlagsPerRound);
                        int i = 0;
                        var currentTasks = new List<CheckerTask>();
                        foreach (var team in activeTeams)
                        {
                            double taskOffset = (i * teamStepLength) + serviceOffset + variantOffset;
                            var checkerTask = new CheckerTask(
                                0,
                                service.Checkers[i % service.Checkers.Length],
                                CheckerTaskMethod.getflag,
                                team.Address ?? $"team{team.Id}.{configuration.DnsSuffix}",
                                service.Id,
                                service.Name,
                                team.Id,
                                team.Name,
                                oldRoundId,
                                round.Id,
                                new Flag(team.Id, service.Id, variantIndex, oldRoundId, 0).ToString(configuration.FlagSigningKey, configuration.Encoding),
                                taskStart.AddSeconds(taskOffset),
                                (int)(quarterLength * 1000),
                                configuration.RoundLengthInSeconds,
                                variantIndex,
                                variantIndex % service.FlagVariants,
                                CheckerResult.INTERNAL_ERROR,
                                null,
                                null,
                                CheckerTaskLaunchStatus.New);
                            currentTasks.Add(checkerTask);
                            i += 1;
                        }

                        this.FisherYatesShuffleTaskStarts(currentTasks);
                        tasks.AddRange(currentTasks);
                    }
                }
            }

            await this.InsertCheckerTasks(tasks);
        }

        public async Task InsertRetrieveCurrentNoisesTasks(
            Round round,
            Team[] activeTeams,
            Service[] activeServices,
            Configuration configuration)
        {
            // getnoises are started in Q3
            double quarterLength = configuration.RoundLengthInSeconds / 4;
            int tasksCount = 0;
            foreach (var service in activeServices)
            {
                tasksCount += (int)service.NoisesPerRound * activeTeams.Length;
            }

            if (tasksCount == 0)
            {
                return;
            }

            var tasks = new List<CheckerTask>(tasksCount);
            var taskStart = round.Quarter3;
            double teamStepLength = (quarterLength - 2) / activeTeams.Length;
            double serviceStepLength = teamStepLength / activeServices.Length;
            foreach (var service in activeServices)
            {
                double serviceOffset = (service.Id - 1) * serviceStepLength;
                for (int variantIndex = 0; variantIndex < service.NoisesPerRound; variantIndex++)
                {
                    double variantOffset = variantIndex * (serviceStepLength / service.NoisesPerRound);
                    int i = 0;
                    var currentTasks = new List<CheckerTask>();
                    foreach (var team in activeTeams)
                    {
                        double taskOffset = (i * teamStepLength) + serviceOffset + variantOffset;

                        // this.logger.LogDebug($"InsertRetrieveCurrentNoisesTasks Q3 taskOffset={taskOffset}");
                        var checkerTask = new CheckerTask(
                            0,
                            service.Checkers[i % service.Checkers.Length],
                            CheckerTaskMethod.getnoise,
                            team.Address ?? $"team{team.Id}.{configuration.DnsSuffix}",
                            service.Id,
                            service.Name,
                            team.Id,
                            team.Name,
                            round.Id,
                            round.Id,
                            null,
                            taskStart.AddSeconds(taskOffset),
                            (int)(quarterLength * 1000),
                            configuration.RoundLengthInSeconds,
                            variantIndex,
                            variantIndex % service.NoiseVariants,
                            CheckerResult.INTERNAL_ERROR,
                            null,
                            null,
                            CheckerTaskLaunchStatus.New);
                        currentTasks.Add(checkerTask);
                        i += 1;
                    }

                    this.FisherYatesShuffleTaskStarts(currentTasks);
                    tasks.AddRange(currentTasks);
                }
            }

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

        private void FisherYatesShuffleTaskStarts(List<CheckerTask> tasks)
        {
            Random random = new Random();
            for (int i = 0; i < (tasks.Count - 1); i++)
            {
                int r = i + random.Next(tasks.Count - i);
                var task = tasks[r];

                // TODO this shouldn't be a record, I guess
                tasks[r] = tasks[r] with
                {
                    StartTime = tasks[i].StartTime
                };
                tasks[i] = tasks[i] with
                {
                    StartTime = task.StartTime
                };
            }
        }
    }
}
