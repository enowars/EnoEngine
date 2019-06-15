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
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

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

    public class TeamServiceStates
    {
        public long Ups { get; set; }
        public long Recoverings { get; set; }
    }

    public interface IEnoDatabase
    {
        DBInitializationResult ApplyConfig(JsonConfiguration configuration);
        Task<FlagSubmissionResult> InsertSubmittedFlag(string flag, string attackerAddressPrefix, JsonConfiguration config);
        Task<(Round, List<Flag>, List<Noise>, List<Havoc>)> CreateNewRound(DateTime begin, DateTime q2, DateTime q3, DateTime q4, DateTime end);
        Task RecordServiceStates(long roundId);
        Task InsertPutFlagsTasks(long roundId, DateTime firstFlagTime, JsonConfiguration config);
        Task InsertPutNoisesTasks(DateTime firstFlagTime, IEnumerable<Noise> currentNoises, JsonConfiguration config);
        Task InsertHavocsTasks(long roundId, DateTime begin, JsonConfiguration config);
        Task InsertRetrieveCurrentFlagsTasks(DateTime q3, IEnumerable<Flag> currentFlags, JsonConfiguration config);
        Task InsertRetrieveOldFlagsTasks(Round currentRound, int oldRoundsCount, JsonConfiguration config);
        Task InsertRetrieveCurrentNoisesTasks(DateTime q3, IEnumerable<Noise> currentNoise, JsonConfiguration config);
        Task<List<CheckerTask>> RetrievePendingCheckerTasks(int maxAmount);
        Task CalculatedAllPoints(ServiceProvider serviceProvider, long roundId, JsonConfiguration config);
        EnoEngineScoreboard GetCurrentScoreboard(long roundId);
        Task UpdateTeamServiceStatsAndFillSnapshot(Service service, long teamsCount, long roundId, long teamId,
            ServiceStatsSnapshot oldSnapshot, ServiceStatsSnapshot newSnapshot,
            TeamServiceStates stableServiceState, TeamServiceStates volatileServiceState);
        void Migrate();
        Task UpdateTaskCheckerTaskResults(Memory<CheckerTask> tasks);
    }

    public class EnoDatabase : IEnoDatabase
    {
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoDatabase));
        private readonly EnoDatabaseContext _context;

        public EnoDatabase(EnoDatabaseContext context)
        {
            _context = context;
        }

        public DBInitializationResult ApplyConfig(JsonConfiguration config)
        {
            Logger.LogTrace(new EnoLogMessage()
            {
                Message = "Applying configuration to database",
                Function = nameof(ApplyConfig),
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

            Migrate();
            return FillDatabase(config);
        }

        public void Migrate()
        {
            var pendingMigrations = _context.Database.GetPendingMigrations().Count();
            if (pendingMigrations > 0)
            {
                Logger.LogInfo(new EnoLogMessage()
                {
                    Message = $"Applying {pendingMigrations} migration(s)",
                    Function = nameof(Migrate),
                    Module = nameof(EnoDatabase)
                });
                _context.Database.Migrate();
                _context.SaveChanges();
                Logger.LogDebug(new EnoLogMessage()
                {
                    Message = $"Database migration complete",
                    Function = nameof(Migrate),
                    Module = nameof(EnoDatabase)
                });
            }
            else
            {
                Logger.LogDebug(new EnoLogMessage()
                {
                    Message = $"No pending migrations",
                    Function = nameof(Migrate),
                    Module = nameof(EnoDatabase)
                });
            }
        }

        private DBInitializationResult FillDatabase(JsonConfiguration config)
        {
            var staleDbTeamIds = _context.Teams.Select(t => t.Id).ToList();

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
                var dbTeam = _context.Teams
                    .Where(t => t.Id == team.Id)
                    .SingleOrDefault();
                if (dbTeam == null)
                {
                    Logger.LogInfo(new EnoLogMessage()
                    {
                        Message = $"Adding team {team.Name}({team.Id})",
                        Module = nameof(EnoDatabase),
                        TeamName = team.Name,
                        Function = nameof(FillDatabase),
                    });
                    _context.Teams.Add(new Team()
                    {
                        TeamSubnet = teamSubnet,
                        Name = team.Name,
                        Id = team.Id,
                        Active = team.Active
                    });
                }
                else
                {
                    dbTeam.TeamSubnet = teamSubnet;
                    dbTeam.Name = team.Name;
                    dbTeam.Id = team.Id;
                    dbTeam.Active = team.Active;
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
            var staleDbServiceIds = _context.Services.Select(t => t.Id).ToList();
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
                if (service.HavocsPerRound < 0)
                    return new DBInitializationResult
                    {
                        Success = false,
                        ErrorMessage = $"Service {service.Name}: HavocsPerRound < 0"
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
                var dbService = _context.Services
                    .Where(s => s.Id == service.Id)
                    .SingleOrDefault();
                if (dbService == null)
                {
                    Logger.LogInfo(new EnoLogMessage()
                    {
                        Message = $"Adding service {service.Name}",
                        Module = nameof(EnoDatabase),
                        Function = nameof(FillDatabase),
                        ServiceName = service.Name
                    });
                    _context.Services.Add(new Service()
                    {
                        Id = service.Id,
                        Name = service.Name,
                        FlagsPerRound = service.FlagsPerRound,
                        NoisesPerRound = service.NoisesPerRound,
                        HavocsPerRound = service.HavocsPerRound,
                        Active = service.Active
                    });
                }
                else
                {
                    dbService.Name = service.Name;
                    dbService.FlagsPerRound = service.FlagsPerRound;
                    dbService.NoisesPerRound = service.NoisesPerRound;
                    dbService.HavocsPerRound = service.HavocsPerRound;
                    dbService.Active = service.Active;
                    staleDbServiceIds.Remove(dbService.Id);
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

            _context.SaveChanges(); // Save so that the services and teams receive proper IDs
            foreach (var service in _context.Services)
            {
                foreach (var team in _context.Teams)
                {
                    var stats = _context.ServiceStats
                        .Where(ss => ss.TeamId == team.Id)
                        .Where(ss => ss.ServiceId == service.Id)
                        .SingleOrDefault();
                    if (stats == null)
                    {
                        _context.ServiceStats.Add(new ServiceStats()
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
            _context.SaveChanges();
            return new DBInitializationResult
            {
                Success = true
            };
        }

        public async Task<FlagSubmissionResult> InsertSubmittedFlag(string flag, string attackerAddressPrefix, JsonConfiguration config) //TODO more logs
        {
            while (true)
            {
                try
                {
                    var dbFlag = await _context.Flags
                            .Where(f => f.StringRepresentation == flag)
                            .Include(f => f.Owner)
                            .AsNoTracking()
                            .SingleOrDefaultAsync();
                    var dbAttackerTeam = await _context.Teams
                        .Where(t => t.TeamSubnet == attackerAddressPrefix)
                        .AsNoTracking()
                        .SingleOrDefaultAsync();
                    var currentRound = await _context.Rounds
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

                    var submittedFlag = await _context.SubmittedFlags
                        .Where(f => f.FlagId == dbFlag.Id && f.AttackerTeamId == dbAttackerTeam.Id)
                        .SingleOrDefaultAsync();

                    if (submittedFlag != null)
                    {
                        submittedFlag.SubmissionsCount += 1;
                        _context.SaveChanges();
                        return FlagSubmissionResult.Duplicate;
                    }
                    _context.SubmittedFlags.Add(new SubmittedFlag()
                    {
                        FlagId = dbFlag.Id,
                        AttackerTeamId = dbAttackerTeam.Id,
                        SubmissionsCount = 1,
                        RoundId = currentRound.Id
                    });
                    _context.SaveChanges();
                    return FlagSubmissionResult.Ok;
                }
                catch (DbUpdateException) { }
            }
        }

        public async Task<(Round, List<Flag>, List<Noise>, List<Havoc>)> CreateNewRound(DateTime begin, DateTime q2, DateTime q3, DateTime q4, DateTime end)
        {
            var round = new Round()
            {
                Begin = begin,
                Quarter2 = q2,
                Quarter3 = q3,
                Quarter4 = q4,
                End = end
            };
            _context.Rounds.Add(round);
            var teams = await _context.Teams
                .ToArrayAsync();
            var services = await _context.Services
                .ToArrayAsync();
            var flags = GenerateFlagsForRound(round, teams, services);
            var noises = GenerateNoisesForRound(round, teams, services);
            var havocs = GenerateHavocsForRound(round, teams, services);
            _context.SaveChanges();
            return (round, flags, noises, havocs);
        }

        private List<Flag> GenerateFlagsForRound(Round round, Team[] teams, Service[] services)
        {
            List<Flag> newFlags = new List<Flag>();
            foreach (var team in teams)
            {
                foreach (var service in services)
                {
                    for (int i = 0; i < service.FlagsPerRound; i++)
                    {
                        var flag = new Flag()
                        {
                            Owner = team,
                            StringRepresentation = EnoCoreUtils.GenerateSignedFlag((int)round.Id, (int)team.Id),
                            ServiceId = service.Id,
                            RoundOffset = i,
                            GameRound = round
                        };
                        newFlags.Add(flag);
                    }
                }
            }
            _context.Flags.AddRange(newFlags);
            return newFlags;
        }

        private List<Noise> GenerateNoisesForRound(Round round, Team[] teams, Service[] services)
        {
            List<Noise> newNoises = new List<Noise>();
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
                            ServiceId = service.Id,
                            RoundOffset = i,
                            GameRound = round
                        };
                        newNoises.Add(noise);
                    }
                }
            }
            _context.Noises.AddRange(newNoises);
            return newNoises;
        }

        private List<Havoc> GenerateHavocsForRound(Round round, Team[] teams, Service[] services)
        {
            List<Havoc> newHavocs = new List<Havoc>();
            foreach (var team in teams)
            {
                foreach (var service in services)
                {
                    for (int i = 0; i < service.HavocsPerRound; i++)
                    {
                        var havoc = new Havoc()
                        {
                            Owner = team,
                            StringRepresentation = EnoCoreUtils.GenerateSignedNoise((int)round.Id, (int)team.Id),
                            ServiceId = service.Id,
                            GameRound = round
                        };
                        newHavocs.Add(havoc);
                    }
                }
            }
            _context.Havocs.AddRange(newHavocs);
            return newHavocs;
        }

        private async Task InsertCheckerTasks(IEnumerable<CheckerTask> tasks)
        {
            _context.AddRange(tasks);
            await _context.SaveChangesAsync();
        }

        public async Task<List<CheckerTask>> RetrievePendingCheckerTasks(int maxAmount)
        {
            var tasks = await _context.CheckerTasks
                .Where(t => t.CheckerTaskLaunchStatus == CheckerTaskLaunchStatus.New)
                .OrderBy(t => t.StartTime)
                .Take(maxAmount)
                .ToListAsync();
            // TODO update launch status without delaying operation
            tasks.ForEach((t) => t.CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.Launched);
            await _context.SaveChangesAsync();
            return tasks;
        }

        public async Task InsertPutFlagsTasks(long roundId, DateTime firstFlagTime, JsonConfiguration config)
        {

            var currentFlags = await _context.Flags
                .Where(f => f.GameRoundId == roundId)
                .Include(f => f.Service)
                .Include(f => f.Owner)
                .ToArrayAsync();

            int maxRunningTime = config.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentFlags.Count();
            var tasks = new CheckerTask[currentFlags.Count()];
            int i = 0;
            foreach (var flag in currentFlags)
            {
                var checkers = config.Checkers[flag.ServiceId];
                var checkerTask = new CheckerTask()
                {
                    Address = $"service{flag.ServiceId}.team{flag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.StringRepresentation,
                    RelatedRoundId = flag.GameRoundId,
                    CurrentRoundId = flag.GameRoundId,
                    StartTime = firstFlagTime,
                    TaskIndex = flag.RoundOffset,
                    TaskType = "putflag",
                    TeamName = flag.Owner.Name,
                    ServiceId = flag.ServiceId,
                    TeamId = flag.OwnerId,
                    ServiceName = flag.Service.Name,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                    RoundLength = config.RoundLengthInSeconds
                };
                tasks[i] = checkerTask;
                flag.PutTask = checkerTask;
                firstFlagTime = firstFlagTime.AddSeconds(timeDiff);
                i++;
            }

            var tasks_start_time = tasks.Select(x => x.StartTime).ToList();
            tasks_start_time = EnoCoreUtils.Shuffle(tasks_start_time).ToList();
            tasks = tasks_start_time.Zip(tasks, (a,b) => {b.StartTime = a; return b;}).ToArray();

            await InsertCheckerTasks(tasks);
        }

        public async Task InsertPutNoisesTasks(DateTime firstFlagTime, IEnumerable<Noise> currentNoises, JsonConfiguration config)
        {
            int maxRunningTime = config.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentNoises.Count();

            var tasks = new List<CheckerTask>(currentNoises.Count());
            int i = 0;
            foreach (var noise in currentNoises)
            {
                var checkers = config.Checkers[noise.ServiceId];
                tasks.Add(new CheckerTask()
                {
                    Address = $"service{noise.ServiceId}.team{noise.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = noise.StringRepresentation,
                    RelatedRoundId = noise.GameRoundId,
                    CurrentRoundId = noise.GameRoundId,
                    StartTime = firstFlagTime,
                    TaskIndex = noise.RoundOffset,
                    TaskType = "putnoise",
                    TeamName = noise.Owner.Name,
                    ServiceId = noise.ServiceId,
                    TeamId = noise.OwnerId,
                    ServiceName = noise.Service.Name,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                    RoundLength = config.RoundLengthInSeconds
                });
                firstFlagTime = firstFlagTime.AddSeconds(timeDiff);
                i++;
            }

            var tasks_start_time = tasks.Select(x => x.StartTime).ToList();
            tasks_start_time = EnoCoreUtils.Shuffle(tasks_start_time).ToList();
            tasks = tasks_start_time.Zip(tasks, (a, b) => { b.StartTime = a; return b; }).ToList();

            await InsertCheckerTasks(tasks);
        }

        public async Task InsertHavocsTasks(long roundId, DateTime begin, JsonConfiguration config)
        {
            int quarterRound = config.RoundLengthInSeconds / 4;

            var currentHavocs = await _context.Havocs
                .Where(f => f.GameRoundId == roundId)
                .Include(f => f.Service)
                .Include(f => f.Owner)
                .ToArrayAsync();
            double timeDiff = (double)quarterRound * 3 / currentHavocs.Count();
            List<CheckerTask> havocTasks = new List<CheckerTask>(currentHavocs.Count());
            int i = 0;
            foreach (var havoc in currentHavocs)
            {
                var checkers = config.Checkers[havoc.ServiceId];
                var task = new CheckerTask()
                {
                    Address = $"service{havoc.ServiceId}.team{havoc.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = quarterRound,
                    Payload = havoc.StringRepresentation,
                    RelatedRoundId = havoc.GameRoundId,
                    CurrentRoundId = roundId,
                    StartTime = begin,
                    TaskIndex = 0,
                    TaskType = "havoc",
                    TeamName = havoc.Owner.Name,
                    ServiceId = havoc.ServiceId,
                    TeamId = havoc.OwnerId,
                    ServiceName = havoc.Service.Name,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                    RoundLength = config.RoundLengthInSeconds
                };
                havocTasks.Add(task);
                begin = begin.AddSeconds(timeDiff);
                i++;
            }
            var tasks_start_time = havocTasks.Select(x => x.StartTime).ToList();
            tasks_start_time = EnoCoreUtils.Shuffle(tasks_start_time).ToList();
            havocTasks = tasks_start_time.Zip(havocTasks, (a,b) => {b.StartTime = a; return b;}).ToList();

            await InsertCheckerTasks(havocTasks);
        }

        public async Task InsertRetrieveCurrentFlagsTasks(DateTime q3, IEnumerable<Flag> currentFlags, JsonConfiguration config)
        {
            int maxRunningTime = config.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentFlags.Count();
            var tasks = new List<CheckerTask>(currentFlags.Count());
            int i = 0;
            foreach (var flag in currentFlags)
            {
                var checkers = config.Checkers[flag.ServiceId];
                tasks.Add(new CheckerTask()
                {
                    Address = $"service{flag.ServiceId}.team{flag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.StringRepresentation,
                    CurrentRoundId = flag.GameRoundId,
                    RelatedRoundId = flag.GameRoundId,
                    StartTime = q3,
                    TaskIndex = flag.RoundOffset,
                    TaskType = "getflag",
                    TeamName = flag.Owner.Name,
                    TeamId = flag.OwnerId,
                    ServiceName = flag.Service.Name,
                    ServiceId = flag.ServiceId,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                    RoundLength = config.RoundLengthInSeconds
                });
                q3 = q3.AddSeconds(timeDiff);
                i++;
            }
            var tasks_start_time = tasks.Select(x => x.StartTime).ToList();
            tasks_start_time = EnoCoreUtils.Shuffle(tasks_start_time).ToList();
            tasks = tasks_start_time.Zip(tasks, (a, b) => { b.StartTime = a; return b; }).ToList();

            await InsertCheckerTasks(tasks);
        }

        public async Task InsertRetrieveOldFlagsTasks(Round currentRound, int oldRoundsCount, JsonConfiguration config)
        {
            int quarterRound = config.RoundLengthInSeconds / 4;

            var oldFlags = await _context.Flags
                .Where(f => f.GameRoundId + oldRoundsCount >= currentRound.Id)
                .Where(f => f.GameRoundId != currentRound.Id)
                .Where(f => f.PutTaskId != null)
                .Include(f => f.PutTask)
                .Where(f => f.PutTask.CheckerResult != CheckerResult.CheckerError)
                .Include(f => f.Owner)
                .Include(f => f.Service)
                .AsNoTracking()
                .ToArrayAsync();
            List<CheckerTask> oldFlagsCheckerTasks = new List<CheckerTask>(oldFlags.Count());
            double timeDiff = (double)quarterRound * 3 / oldFlags.Count();
            DateTime time = currentRound.Begin;
            int i = 0;
            foreach (var oldFlag in oldFlags)
            {
                var checkers = config.Checkers[oldFlag.ServiceId];
                var task = new CheckerTask()
                {
                    Address = $"service{oldFlag.ServiceId}.team{oldFlag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
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
                i++;
            }

            var tasks_start_time = oldFlagsCheckerTasks.Select(x => x.StartTime).ToList();
            tasks_start_time = EnoCoreUtils.Shuffle(tasks_start_time).ToList();
            oldFlagsCheckerTasks = tasks_start_time.Zip(oldFlagsCheckerTasks, (a,b) => {b.StartTime = a; return b;}).ToList();

            await InsertCheckerTasks(oldFlagsCheckerTasks);
        }

        public async Task InsertRetrieveCurrentNoisesTasks(DateTime q3, IEnumerable<Noise> currentNoises, JsonConfiguration config)
        {
            int maxRunningTime = config.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentNoises.Count();
            var tasks = new List<CheckerTask>(currentNoises.Count());
            int i = 0;
            foreach (var flag in currentNoises)
            {
                var checkers = config.Checkers[flag.ServiceId];
                tasks.Add(new CheckerTask()
                {
                    Address = $"service{flag.ServiceId}.team{flag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.StringRepresentation,
                    CurrentRoundId = flag.GameRoundId,
                    RelatedRoundId = flag.GameRoundId,
                    StartTime = q3,
                    TaskIndex = flag.RoundOffset,
                    TaskType = "getnoise",
                    TeamName = flag.Owner.Name,
                    TeamId = flag.OwnerId,
                    ServiceName = flag.Service.Name,
                    ServiceId = flag.ServiceId,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                    RoundLength = config.RoundLengthInSeconds
                });
                q3 = q3.AddSeconds(timeDiff);
                i++;
            }

            var tasks_start_time = tasks.Select(x => x.StartTime).ToList();
            tasks_start_time = EnoCoreUtils.Shuffle(tasks_start_time).ToList();
            tasks = tasks_start_time.Zip(tasks, (a, b) => { b.StartTime = a; return b; }).ToList();

            await InsertCheckerTasks(tasks);
        }

        public async Task RecordServiceStates(long roundId)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var round = await _context.Rounds
                .Where(r => r.Id == roundId)
                .AsNoTracking()
                .SingleAsync();

            var teams = await _context.Teams.AsNoTracking().ToArrayAsync();
            var services = await _context.Services.AsNoTracking().ToArrayAsync();
            foreach (var team in teams)
            {
                foreach (var service in services)
                {
                    var reportedStatus = await ComputeServiceStatus(team, service, roundId);
                    var roundTeamServiceState = new RoundTeamServiceState()
                    {
                        GameRoundId = round.Id,
                        ServiceId = service.Id,
                        TeamId = team.Id,
                        Status = reportedStatus
                    };
                    _context.RoundTeamServiceStates.Add(roundTeamServiceState);

                    (await _context.ServiceStats
                        .Where(ss => ss.TeamId == team.Id && ss.ServiceId == service.Id)
                        .SingleAsync()).Status = reportedStatus;
                }
            }
            await _context.SaveChangesAsync();
            stopWatch.Stop();
            Console.WriteLine($"RecordServiceStates took {stopWatch.Elapsed.TotalMilliseconds}ms");
        }

        public async Task CalculatedAllPoints(ServiceProvider serviceProvider, long roundId, JsonConfiguration config)
        {
            long newLatestSnapshotRoundId = Math.Max(0, roundId - config.FlagValidityInRounds - 1);
            var services = await _context.Services
                .AsNoTracking()
                .ToArrayAsync();
            var teams = await _context.Teams
                .OrderBy(t => t.Id)
                .ToArrayAsync();

            foreach (var service in services)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                await CalculateServiceScores(serviceProvider, teams, roundId, service, newLatestSnapshotRoundId);
                stopWatch.Stop();
                Console.WriteLine($"CalculateServiceScores {service.Name} took {stopWatch.Elapsed.TotalMilliseconds}ms");
            }

            // calculate the total points
            var sums = await _context.ServiceStats
                .GroupBy(ss => ss.TeamId)
                .Select(g => g.OrderBy(ss => ss.TeamId).Sum(ss => ss.ServiceLevelAgreementPoints + ss.LostDefensePoints + ss.AttackPoints))
                .ToArrayAsync();
            for (int i = 0; i< teams.Length; i++)
            {
                teams[i].AttackPoints = sums[i];
            }
            await _context.SaveChangesAsync();

        }

        private async Task CalculateServiceScores(ServiceProvider serviceProvider, Team[] teams, long roundId, Service service, long newLatestSnapshotRoundId)
        {
            ServiceStatsSnapshot[] oldSnapshots;
            long oldSnapshotsRoundId;
            ServiceStatsSnapshot[] newSnapshots;


            oldSnapshots = await _context.ServiceStatsSnapshots
                .Where(sss => sss.ServiceId == service.Id)
                .GroupBy(sss => sss.TeamId)
                .Select(g => g.OrderBy(sss => sss.Id).Last())
                .AsNoTracking()
                .ToArrayAsync();
            if (oldSnapshots.Length == 0)
            {
                oldSnapshots = null;
                oldSnapshotsRoundId = 0;
            }
            else
            {
                oldSnapshotsRoundId = oldSnapshots[0].RoundId;
            }

            if (newLatestSnapshotRoundId != 0)
            {
                newSnapshots = teams.Select((t, i) => new ServiceStatsSnapshot()
                {
                    AttackPoints = oldSnapshots?[i].AttackPoints ?? 0,
                    LostDefensePoints = oldSnapshots?[i].LostDefensePoints ?? 0,
                    ServiceLevelAgreementPoints = oldSnapshots?[i].LostDefensePoints ?? 0,
                    RoundId = newLatestSnapshotRoundId,
                    ServiceId = service.Id,
                    TeamId = t.Id
                }).ToArray();
            }
            else
            {
                newSnapshots = null;
            }

            var stableServiceStates = await _context.RoundTeamServiceStates
                .Where(rtts => rtts.GameRoundId > oldSnapshotsRoundId)
                .Where(rtts => rtts.GameRoundId <= newLatestSnapshotRoundId)
                .GroupBy(rtts => rtts.TeamId)
                .Select(g => new TeamServiceStates()
                {
                    Ups = g.Where(rtts => rtts.Status == ServiceStatus.Ok).Count(),
                    Recoverings = g.Where(rtts => rtts.Status == ServiceStatus.Recovering).Count()
                })
                .AsNoTracking()
                .ToArrayAsync();
            if (stableServiceStates.Length == 0) // stable stats are empty in the first few rounds
            {
                stableServiceStates = null;
            }

            var volatileServiceStates = await _context.RoundTeamServiceStates
                .Where(rtts => rtts.GameRoundId <= roundId)
                .Where(rtts => rtts.GameRoundId >= newLatestSnapshotRoundId)
                .GroupBy(rtts => rtts.TeamId)
                .Select(g => new TeamServiceStates()
                {
                    Ups = g.Where(rtts => rtts.Status == ServiceStatus.Ok).Count(),
                    Recoverings = g.Where(rtts => rtts.Status == ServiceStatus.Recovering).Count()
                })
                .AsNoTracking()
                .ToArrayAsync();

            var tasks = new HashSet<Task>();
            var teamserviceStats = new TeamServiceStates[teams.Length];
            for (int i = 0; i < teams.Length; i++)
            {
                int localIndex = i; //prevent i from being incremented before the task runs
                var team = teams[localIndex];
                if (tasks.Count < 16)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        using (var scope = serviceProvider.CreateScope())
                        {
                            await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().UpdateTeamServiceStatsAndFillSnapshot(service, teams.Length, roundId, team.Id,
                                oldSnapshots?[localIndex] ?? null, newSnapshots?[localIndex] ?? null,
                                stableServiceStates?[localIndex] ?? null, volatileServiceStates[localIndex] ?? null);
                        }
                    }));
                }
                else
                {
                    Task finished = await Task.WhenAny(tasks);
                    tasks.Remove(finished);
                    tasks.Add(Task.Run(async () => {
                        using (var scope = serviceProvider.CreateScope())
                        {
                            await scope.ServiceProvider.GetRequiredService<IEnoDatabase>().UpdateTeamServiceStatsAndFillSnapshot(service, teams.Length, roundId, team.Id,
                                oldSnapshots?[localIndex] ?? null, newSnapshots?[localIndex] ?? null,
                                stableServiceStates?[localIndex] ?? null, volatileServiceStates[localIndex] ?? null);
                        }
                    }));
                }
            }

            if (newSnapshots != null)
            {
                _context.ServiceStatsSnapshots.AddRange(newSnapshots);
                await _context.SaveChangesAsync();
            }
            await Task.WhenAll(tasks);
        }

        public async Task UpdateTeamServiceStatsAndFillSnapshot(Service service, long teamsCount, long roundId, long teamId,
            ServiceStatsSnapshot oldSnapshot, ServiceStatsSnapshot newSnapshot,
            TeamServiceStates stableServiceState, TeamServiceStates volatileServiceState)
        {
            var teamServiceStats = await _context.ServiceStats
                .Where(ss => ss.TeamId == teamId)
                .Where(ss => ss.ServiceId == service.Id)
                .SingleAsync();
            teamServiceStats.ServiceLevelAgreementPoints = CalculateTeamSlaScore(teamsCount,
                oldSnapshot, newSnapshot,
                stableServiceState, volatileServiceState);
            //TODO atk
            //TODO def
            await _context.SaveChangesAsync();
        }


        private double CalculateTeamSlaScore(long teamsCount,
            ServiceStatsSnapshot oldSnapshot, ServiceStatsSnapshot newSnapshot,
            TeamServiceStates stableServiceState, TeamServiceStates volatileServiceStates)
        {
            double serviceSla = 0;
            if (oldSnapshot != null)
            {
                serviceSla += oldSnapshot.ServiceLevelAgreementPoints;
            }
            if (newSnapshot != null)
            {
                serviceSla += (stableServiceState.Ups + (stableServiceState.Recoverings * 0.5)) * Math.Sqrt(teamsCount);
                newSnapshot.ServiceLevelAgreementPoints = serviceSla;
            }
            serviceSla += (volatileServiceStates.Ups + (volatileServiceStates.Recoverings * 0.5)) * Math.Sqrt(teamsCount);
            return serviceSla;
        }

        private async Task<ServiceStatus> ComputeServiceStatus(Team team, Service service, long roundId)
        {
            var currentRoundTasks = await _context.CheckerTasks
                .Where(ct => ct.CurrentRoundId == roundId)
                .Where(ct => ct.RelatedRoundId == roundId)
                .Where(ct => ct.TeamId == team.Id)
                .Where(ct => ct.ServiceId == service.Id)
                .AsNoTracking()
                .ToArrayAsync();

            if (currentRoundTasks.Length == 0)
            {
                return ServiceStatus.CheckerError;
            }
            ServiceStatus bestServiceStatus = ServiceStatus.Ok;
            foreach (var task in currentRoundTasks)
            {
                switch (task.CheckerResult)
                {
                    case CheckerResult.Ok:
                        continue;
                    case CheckerResult.Mumble:
                        if (bestServiceStatus == ServiceStatus.Ok)
                        {
                            bestServiceStatus = ServiceStatus.Mumble;
                        }
                        continue;
                    case CheckerResult.Down:
                        if (bestServiceStatus == ServiceStatus.Ok || bestServiceStatus == ServiceStatus.Mumble)
                        {
                            bestServiceStatus = ServiceStatus.Down;
                        }
                        continue;
                    default:
                        return ServiceStatus.CheckerError;
                }
            }
            if (bestServiceStatus != ServiceStatus.Ok)
            {
                return bestServiceStatus;
            }

            // Current round was Ok, let's check the old ones
            var oldRoundTasks = _context.CheckerTasks
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

        public EnoEngineScoreboard GetCurrentScoreboard(long roundId)
        {
            var teams = _context.Teams.AsNoTracking().OrderByDescending(t => t.TotalPoints).ToList();
            var round = _context.Rounds
                .AsNoTracking()
                .Where(r => r.Id == roundId)
                .LastOrDefault();
            var services = _context.Services.AsNoTracking().ToList();
            var scoreboard = new EnoEngineScoreboard(round, services);
            foreach (var team in teams)
            {
                var details = _context.ServiceStats
                    .Where(ss => ss.TeamId == team.Id)
                    .AsNoTracking()
                    .ToList();
                scoreboard.Teams.Add(new EnoEngineScoreboardEntry(team, details));
            }
            return scoreboard;
        }

        public async Task UpdateTaskCheckerTaskResults(Memory<CheckerTask> tasks)
        {
            var tasksEnumerable = MemoryMarshal.ToEnumerable<CheckerTask>(tasks);
            _context.UpdateRange(tasksEnumerable);
            await _context.SaveChangesAsync();
        }
    }
}
