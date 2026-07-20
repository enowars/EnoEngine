using System.Threading;

namespace EnoDatabase;

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
            .SingleAsync();
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
            end,
            RoundStatus.Prepared);
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

    public async Task<Round?> GetLastRound()
    {
        var round = await this.context.Rounds
            .OrderByDescending(f => f.Id)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return round;
    }

    public async Task<Round?> GetFirstUnscoreRound(CancellationToken token)
    {
        return await this.context.Rounds
            .OrderBy(f => f.Id)
            .Where(e => e.Status == RoundStatus.Finished)
            .FirstOrDefaultAsync(token);
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
                        new Flag(team.Id, service.Id, variantIndex, round.Id).ToString(configuration.FlagSigningKey, configuration.Encoding),
                        taskStart!.Value.AddSeconds(taskOffset),
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

                FisherYatesShuffleTaskStarts(currentTasks);
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
        var taskStart = round.Begin!.Value;
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

                FisherYatesShuffleTaskStarts(currentTasks);
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
                        taskStart.Value.AddSeconds(taskOffset),
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

                FisherYatesShuffleTaskStarts(currentTasks);
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

        /*TODO
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
                        new Flag(team.Id, service.Id, variantIndex, round.Id).ToString(configuration.FlagSigningKey, configuration.Encoding),
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

                FisherYatesShuffleTaskStarts(currentTasks);
                tasks.AddRange(currentTasks);
            }
        }

        await this.InsertCheckerTasks(tasks);
        */
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
        /*TODO
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
                            new Flag(team.Id, service.Id, variantIndex, oldRoundId).ToString(configuration.FlagSigningKey, configuration.Encoding),
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

                    FisherYatesShuffleTaskStarts(currentTasks);
                    tasks.AddRange(currentTasks);
                }
            }
        }

        await this.InsertCheckerTasks(tasks);
        */
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

        /*TODO
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

                FisherYatesShuffleTaskStarts(currentTasks);
                tasks.AddRange(currentTasks);
            }
        }

        await this.InsertCheckerTasks(tasks);
        */
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
            .FirstAsync();
    }

    private static void FisherYatesShuffleTaskStarts(List<CheckerTask> tasks)
    {
        Random random = new Random();
        for (int i = 0; i < (tasks.Count - 1); i++)
        {
            int r = i + random.Next(tasks.Count - i);
            var task = tasks[r];

            // TODO this shouldn't be a record, I guess
            tasks[r] = tasks[r] with
            {
                StartTime = tasks[i].StartTime,
            };
            tasks[i] = tasks[i] with
            {
                StartTime = task.StartTime,
            };
        }
    }

    private async Task InsertCheckerTasks(IEnumerable<CheckerTask> tasks)
    {
        this.logger.LogDebug($"InsertCheckerTasks inserting {tasks.Count()} tasks");
        this.context.AddRange(tasks);
        await this.context.SaveChangesAsync();
    }
}
