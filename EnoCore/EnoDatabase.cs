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
using System.Net.Sockets;
using System.Data;

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

    public interface IEnoDatabase
    {
        DBInitializationResult ApplyConfig(JsonConfiguration configuration);
        Task ProcessSubmissionsBatch(List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> result)> submissions, long flagValidityInRounds);
        Task<(Round, Round, List<Flag>, List<Noise>, List<Havoc>)> CreateNewRound(DateTime begin, DateTime q2, DateTime q3, DateTime q4, DateTime end);
        Task<Dictionary<(long ServiceId, long TeamId), RoundTeamServiceState>> CalculateRoundTeamServiceStates(IServiceProvider serviceProvider, long roundId);
        Task InsertPutFlagsTasks(long roundId, DateTime firstFlagTime, JsonConfiguration config);
        Task InsertPutNoisesTasks(Round currentRound, IEnumerable<Noise> currentNoises, JsonConfiguration config);
        Task InsertHavocsTasks(long roundId, DateTime begin, JsonConfiguration config);
        Task<Flag[]> RetrieveFlags(int maxAmount);
        Task InsertRetrieveCurrentFlagsTasks(Round round, List<Flag> currentFlags, JsonConfiguration config);
        Task InsertRetrieveOldFlagsTasks(Round currentRound, int oldRoundsCount, JsonConfiguration config);
        Task<long> GetTeamIdByPrefix(string attackerPrefixString);
        Task InsertRetrieveCurrentNoisesTasks(Round currentRound, List<Noise> currentNoises, JsonConfiguration config);
        Task<List<CheckerTask>> RetrievePendingCheckerTasks(int maxAmount);
        Task CalculateTotalPoints();
        Task<Round> GetLastRound();
        EnoEngineScoreboard GetCurrentScoreboard(long roundId);
        /*Task UpdateTeamServiceStatsAndFillSnapshot(Service service, long teamsCount, long roundId, long teamId,
            ServiceStatsSnapshot oldSnapshot, ServiceStatsSnapshot newSnapshot,
            TeamServiceStates stableServiceState, TeamServiceStates volatileServiceState);*/
        void Migrate();
        Task UpdateTaskCheckerTaskResults(Memory<CheckerTask> tasks);
        Task<(long newLatestSnapshotRoundId, long oldSnapshotRoundId, Service[] services, Team[] teams)> GetPointCalculationFrame(JsonConfiguration configuration);
        Task CalculateServiceStats(Team[] teams, Dictionary<(long ServiceId, long TeamId), RoundTeamServiceState> newStates, long roundId, Service service, long oldSnapshotRoundId, long newLatestSnapshotRoundId);
    }

    public class EnoDatabase : IEnoDatabase
    {
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoDatabase));
        private readonly EnoDatabaseContext _context;

        public EnoDatabase(EnoDatabaseContext context, ILogger<EnoDatabase> logger)
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

        public async Task ProcessSubmissionsBatch(List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> result)> submissions, long flagValidityInRounds)
        {
            long okFlags = 0;
            long duplicateFlags = 0;
            long oldFlags = 0;
            var statement = new StringBuilder();
            var acceptedSubmissions = new List<TaskCompletionSource<FlagSubmissionResult>>(submissions.Count);
            var acceptedSubmissionsSet = new HashSet<(long, long)>();
            long roundId = await _context.Rounds
                .OrderByDescending(r => r.Id)
                .Select(r => r.Id)
                .FirstAsync();
            statement.Append("insert into \"SubmittedFlags\" (\"FlagId\", \"AttackerTeamId\", \"RoundId\", \"SubmissionsCount\")\nvalues ");
            foreach (var (flag, attackerTeamId, result) in submissions)
            {
                if (flag.GameRoundId + flagValidityInRounds >= roundId)
                {
                    if (!acceptedSubmissionsSet.Contains((flag.Id, attackerTeamId)))
                    {
                        acceptedSubmissions.Add(result);
                        acceptedSubmissionsSet.Add((flag.Id, attackerTeamId));
                        statement.Append($"({flag.Id}, {attackerTeamId}, {roundId}, 1),");
                    }
                    else
                    {
                        var t = Task.Run(() => result.TrySetResult(FlagSubmissionResult.Duplicate));
                    }
                }
                else
                {
                    oldFlags += 1;
                    var t = Task.Run(() => result.TrySetResult(FlagSubmissionResult.Old)); //TODO can multiple trysetresults cause harm?
                }
            }
            statement.Length--; // Pointers are fun!
            statement.Append("\non conflict (\"AttackerTeamId\",\"FlagId\") do update set \"SubmissionsCount\" = \"SubmittedFlags\".\"SubmissionsCount\" + 1 returning \"Id\", \"AttackerTeamId\", \"RoundId\", \"FlagId\", \"SubmissionsCount\";");
            if (acceptedSubmissions.Count > 0)
            {
                var inserts = await _context.SubmittedFlags.FromSql(statement.ToString()).ToArrayAsync();
                for (int i = 0; i < acceptedSubmissions.Count; i++)
                {
                    var ti = i;
                    if (inserts[i].SubmissionsCount == 1)
                    {
                        var t = Task.Run(() => acceptedSubmissions[ti].TrySetResult(FlagSubmissionResult.Ok));
                        okFlags += 1;
                    }
                    else
                    {
                        var t = Task.Run(() => acceptedSubmissions[ti].TrySetResult(FlagSubmissionResult.Duplicate));
                        duplicateFlags += 1;
                    }
                }
            }
            Logger.LogStatistics(FlagsubmissionBatchProcessedMessage.Create(submissions.Count, okFlags, duplicateFlags, oldFlags));
        }

        public async Task<(Round, Round, List<Flag>, List<Noise>, List<Havoc>)> CreateNewRound(DateTime begin, DateTime q2, DateTime q3, DateTime q4, DateTime end)
        {
            var oldRound = await _context.Rounds
                .OrderBy(r => r.Id)
                .LastOrDefaultAsync();
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
            return (oldRound, round, flags, noises, havocs);
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
                            Entropy = EnoCoreUtils.GenerateFlagEntropy(),
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
                            StringRepresentation = EnoCoreUtils.GenerateNoise(),
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
            Logger.LogDebug(new EnoLogMessage()
            {
                Message = $"InsertCheckerTasks inserting {tasks.Count()} tasks"
            });
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

        public async Task<Flag[]> RetrieveFlags(int maxAmount)
        {
            var flags = await _context.Flags
                .OrderByDescending(f => f.Id)
                .Where(f => f.OwnerId <= 50)
                .AsNoTracking()
                .Take(maxAmount)
                .ToArrayAsync();
            return flags;
        }

        public async Task<Round> GetLastRound(){
            var round = await _context.Rounds
                .OrderByDescending(f => f.Id)
                .FirstOrDefaultAsync();
            return round;
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
            var tasks = new CheckerTask[currentFlags.Length];
            int i = 0;
            foreach (var flag in currentFlags)
            {
                var checkers = config.Checkers[flag.ServiceId];
                var checkerTask = new CheckerTask()
                {
                    Address = $"{flag.Service.Name.ToLower()}.team{flag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.ToString(),
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
                firstFlagTime = firstFlagTime.AddSeconds(timeDiff);
                i++;
            }

            var tasks_start_time = tasks.Select(x => x.StartTime).ToList();
            tasks_start_time = EnoCoreUtils.Shuffle(tasks_start_time).ToList();
            tasks = tasks_start_time.Zip(tasks, (a, b) => { b.StartTime = a; return b; }).ToArray();

            await InsertCheckerTasks(tasks);
        }

        public async Task InsertPutNoisesTasks(Round currentRound, IEnumerable<Noise> currentNoises, JsonConfiguration config)
        {
            int maxRunningTime = config.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentNoises.Count();
            DateTime firstFlagTime = currentRound.Begin;
            var tasks = new List<CheckerTask>(currentNoises.Count());
            int i = 0;
            foreach (var noise in currentNoises)
            {
                var checkers = config.Checkers[noise.ServiceId];
                tasks.Add(new CheckerTask()
                {
                    Address = $"{noise.Service.Name.ToLower()}.team{noise.OwnerId}.{config.DnsSuffix}",
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
                    Address = $"{havoc.Service.Name.ToLower()}.team{havoc.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = quarterRound,
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
            havocTasks = tasks_start_time.Zip(havocTasks, (a, b) => { b.StartTime = a; return b; }).ToList();
            await InsertCheckerTasks(havocTasks);
        }

        public async Task InsertRetrieveCurrentFlagsTasks(Round round, List<Flag> currentFlags, JsonConfiguration config)
        {
            DateTime q3 = round.Quarter3;
            int maxRunningTime = config.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentFlags.Count();
            var tasks = new List<CheckerTask>(currentFlags.Count());
            for (int i = 0; i < currentFlags.Count; i++)
            {
                var flag = currentFlags[i];
                var checkers = config.Checkers[flag.ServiceId];
                tasks.Add(new CheckerTask()
                {
                    Address = $"{flag.Service.Name.ToLower()}.team{flag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.ToString(),
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
                .TagWith("InsertRetrieveOldFlagsTasks:oldFlags")
                .Where(f => f.GameRoundId  >= currentRound.Id - oldRoundsCount) // TODO skipped IDs
                .Where(f => f.GameRoundId != currentRound.Id)
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
                    Address = $"{oldFlag.Service.Name.ToLower()}.team{oldFlag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = quarterRound,
                    Payload = oldFlag.ToString(),
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

            /*
            var tasks_start_time = oldFlagsCheckerTasks.Select(x => x.StartTime).ToList();
            tasks_start_time = EnoCoreUtils.Shuffle(tasks_start_time).ToList();
            oldFlagsCheckerTasks = tasks_start_time.Zip(oldFlagsCheckerTasks, (a, b) => { b.StartTime = a; return b; }).ToList();
            */

            await InsertCheckerTasks(oldFlagsCheckerTasks);
        }

        public async Task InsertRetrieveCurrentNoisesTasks(Round currentRound, List<Noise> currentNoises, JsonConfiguration config)
        {
            int maxRunningTime = config.RoundLengthInSeconds / 4;
            double timeDiff = (maxRunningTime - 5) / (double)currentNoises.Count();
            var tasks = new List<CheckerTask>(currentNoises.Count());
            DateTime q3 = currentRound.Quarter3;
            for (int i = 0; i < currentNoises.Count; i++)
            {
                var noise = currentNoises[i];
                var checkers = config.Checkers[noise.ServiceId];
                tasks.Add(new CheckerTask()
                {
                    Address = $"{noise.Service.Name.ToLower()}.team{noise.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = noise.StringRepresentation,
                    CurrentRoundId = noise.GameRoundId,
                    RelatedRoundId = noise.GameRoundId,
                    StartTime = q3,
                    TaskIndex = noise.RoundOffset,
                    TaskType = "getnoise",
                    TeamName = noise.Owner.Name,
                    TeamId = noise.OwnerId,
                    ServiceName = noise.Service.Name,
                    ServiceId = noise.ServiceId,
                    CheckerTaskLaunchStatus = CheckerTaskLaunchStatus.New,
                    RoundLength = config.RoundLengthInSeconds
                });
                q3 = q3.AddSeconds(timeDiff);
            }

            var tasks_start_time = tasks.Select(x => x.StartTime).ToList();
            tasks_start_time = EnoCoreUtils.Shuffle(tasks_start_time).ToList();
            tasks = tasks_start_time.Zip(tasks, (a, b) => { b.StartTime = a; return b; }).ToList();

            await InsertCheckerTasks(tasks);
        }

        public async Task<Dictionary<(long ServiceId, long TeamId), RoundTeamServiceState>> CalculateRoundTeamServiceStates(IServiceProvider serviceProvider, long roundId)
        {

            var teams = await _context.Teams.AsNoTracking().ToArrayAsync();
            var services = await _context.Services.AsNoTracking().ToArrayAsync();

            var currentRoundWorstResults = await _context.CheckerTasks
                .TagWith("CalculateRoundTeamServiceStates:currentRoundTasks")
                .Where(ct => ct.CurrentRoundId == roundId)
                .Where(ct => ct.RelatedRoundId == roundId)
                .GroupBy(ct => new { ct.ServiceId, ct.TeamId })
                .Select(g => new { g.Key, WorstResult = g.Min(ct => ct.CheckerResult) })
                .AsNoTracking()
                .ToDictionaryAsync(g => g.Key, g => g.WorstResult);

            var oldRoundsWorstResults = await _context.CheckerTasks
                .TagWith("CalculateRoundTeamServiceStates:oldRoundsTasks")
                .Where(ct => ct.CurrentRoundId == roundId)
                .Where(ct => ct.RelatedRoundId != roundId)
                .GroupBy(ct => new { ct.ServiceId, ct.TeamId })
                .Select(g => new { g.Key, WorstResult = g.Min(ct => ct.CheckerResult) })
                .AsNoTracking()
                .ToDictionaryAsync(g => g.Key, g => g.WorstResult);

            var states = new Dictionary<(long ServiceId, long TeamId), RoundTeamServiceState>();
            foreach (var team in teams)
            {
                foreach (var service in services)
                {
                    var key = new { ServiceId = service.Id, TeamId = team.Id };
                    ServiceStatus status = ServiceStatus.CheckerError;
                    if (currentRoundWorstResults.ContainsKey(key))
                    {
                        status = EnoCoreUtils.CheckerResultToServiceStatus(currentRoundWorstResults[key]);
                    }
                    if (status == ServiceStatus.Ok && oldRoundsWorstResults.ContainsKey(key))
                    {
                        if (oldRoundsWorstResults[key] != CheckerResult.Ok)
                        {
                            status = ServiceStatus.Recovering;
                        }
                    }
                    states[(key.ServiceId, key.TeamId)] = new RoundTeamServiceState()
                    {
                        GameRoundId = roundId,
                        ServiceId = key.ServiceId,
                        TeamId = key.TeamId,
                        Status = status
                    };
                }
            }
            _context.RoundTeamServiceStates.AddRange(states.Values);
            await _context.SaveChangesAsync();
            return states;
        }

        public async Task<(long newLatestSnapshotRoundId, long oldSnapshotRoundId, Service[] services, Team[] teams)> GetPointCalculationFrame(JsonConfiguration config)
        {
            var newLatestSnapshotRoundId = await _context.Rounds
                .OrderByDescending(r => r.Id)
                .Skip((int)config.FlagValidityInRounds + 1)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var oldSnapshotRoundId = await _context.Rounds
                .OrderByDescending(r => r.Id)
                .Skip((int)config.FlagValidityInRounds + 2)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var services = await _context.Services
                .AsNoTracking()
                .ToArrayAsync();
            var teams = await _context.Teams
                .AsNoTracking()
                .ToArrayAsync();

            return (newLatestSnapshotRoundId, oldSnapshotRoundId, services, teams);
        }

        public async Task CalculateTotalPoints()
        {
            var stats = await _context.ServiceStats
                .GroupBy(ss => ss.TeamId)
                .Select(g => new {
                    g.Key,
                    AttackPointsSum = g.Sum(s => s.AttackPoints),
                    DefensePointsSum = g.Sum(s => s.LostDefensePoints),
                    SLAPointsSum = g.Sum(s => s.ServiceLevelAgreementPoints)
                })
                .AsNoTracking()
                .ToDictionaryAsync(ss => ss.Key);
            var dbTeams = await _context.Teams
                .ToDictionaryAsync(t => t.Id);
            foreach (var sums in stats)
            {
                var team = dbTeams[sums.Key];
                var sum = sums.Value.AttackPointsSum + sums.Value.DefensePointsSum + sums.Value.SLAPointsSum;
                team.AttackPoints = sums.Value.AttackPointsSum;
                team.LostDefensePoints = sums.Value.DefensePointsSum;
                team.ServiceLevelAgreementPoints = sums.Value.SLAPointsSum;
                team.TotalPoints = sum;
            }
            await _context.SaveChangesAsync();
        }

        public async Task CalculateServiceStats(Team[] teams,
            Dictionary<(long ServiceId, long TeamId), RoundTeamServiceState> newStates, long roundId,
            Service service, long oldSnapshotsRoundId, long newLatestSnapshotRoundId)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var oldSnapshots = await _context.ServiceStatsSnapshots
                .TagWith("CalculateServiceScores:oldSnapshots")
                .Where(sss => sss.RoundId == oldSnapshotsRoundId)
                .Where(sss => sss.ServiceId == service.Id)
                .AsNoTracking()
                .ToDictionaryAsync(sss => sss.TeamId);

            var stableServiceStates = await _context.RoundTeamServiceStates
                .TagWith("CalculateServiceScores:stableServiceStates")
                .Where(rtts => rtts.GameRoundId > oldSnapshotsRoundId)
                .Where(rtts => rtts.GameRoundId <= newLatestSnapshotRoundId)
                .Where(rtts => rtts.ServiceId == service.Id)
                .GroupBy(rtss => new { rtss.TeamId, rtss.Status })
                .Select(rtss => new { rtss.Key, Amount = rtss.Count() })
                .AsNoTracking()
                .ToDictionaryAsync(rtss => rtss.Key);

            var volatileServiceStates = await _context.RoundTeamServiceStates
                .TagWith("CalculateServiceScores:volatileServiceStates")
                .Where(rtts => rtts.GameRoundId <= roundId)
                .Where(rtts => rtts.GameRoundId > newLatestSnapshotRoundId)
                .Where(rtts => rtts.ServiceId == service.Id)
                .GroupBy(rtss => new { rtss.TeamId, rtss.Status })
                .Select(rtss => new { rtss.Key, Amount = rtss.Count() })
                .AsNoTracking()
                .ToDictionaryAsync(rtss => rtss.Key);

            var allCapturesOfFlagsSinceSnapshot = await _context.SubmittedFlags
                .TagWith("CalculateServiceScores:allCapturesOfFlagsSinceSnapshot")
                .Where(sf => sf.RoundId > oldSnapshotsRoundId)
                .Include(sf => sf.Flag)
                .Where(sf => sf.Flag.ServiceId == service.Id)
                .GroupBy(sf => sf.FlagId)
                .Select(g => new { g.Key, Amount = g.Count() })
                .AsNoTracking()
                .ToDictionaryAsync(g => g.Key);

            var stableCaptures = await _context.SubmittedFlags
                .TagWith("CalculateServiceScores:stableCaptures")
                .Where(sf => sf.RoundId > oldSnapshotsRoundId)
                .Where(sf => sf.RoundId <= newLatestSnapshotRoundId)
                .Include(sf => sf.Flag)
                .Where(sf => sf.Flag.ServiceId == service.Id)
                .GroupBy(sf => sf.AttackerTeamId) // this is not an SQL GroupBy, since we need the invidual flags
                .AsNoTracking()
                .ToDictionaryAsync(sf => sf.Key, sf => new { SubmittedFlags = sf.AsEnumerable() });

            var volatileCaptures = await _context.SubmittedFlags
                .TagWith("CalculateServiceScores:volatileCaptures")
                .Where(sf => sf.RoundId <= roundId)
                .Where(sf => sf.RoundId > newLatestSnapshotRoundId)
                .Include(sf => sf.Flag)
                .Where(sf => sf.Flag.ServiceId == service.Id)
                .GroupBy(sf => sf.AttackerTeamId) // this is not an SQL GroupBy, since we need the invidual flags
                .AsNoTracking()
                .ToDictionaryAsync(sf => sf.Key, sf => new { SubmittedFlags = sf.AsEnumerable() });

            var stableLosses = await _context.SubmittedFlags
                .TagWith("CalculateServiceScores:stableLosses")
                .Where(sf => sf.RoundId > oldSnapshotsRoundId)
                .Where(sf => sf.RoundId <= newLatestSnapshotRoundId)
                .Include(sf => sf.Flag)
                .Where(sf => sf.Flag.ServiceId == service.Id)
                .GroupBy(sf => sf.Flag.OwnerId) // this is not an SQL GroupBy, since we need the invidual flags
                .AsNoTracking()
                .ToDictionaryAsync(sf => sf.Key, sf => new { LostFlagIds = sf.AsEnumerable().Select(s => s.FlagId).Distinct() });

            var volatileLosses = await _context.SubmittedFlags
                .TagWith("CalculateServiceScores:volatileLosses")
                .Where(sf => sf.RoundId <= roundId)
                .Where(sf => sf.RoundId > newLatestSnapshotRoundId)
                .Include(sf => sf.Flag)
                .Where(sf => sf.Flag.ServiceId == service.Id)
                .GroupBy(sf => sf.Flag.OwnerId) // this is not an SQL GroupBy, since we need the invidual flags
                .AsNoTracking()
                .ToDictionaryAsync(sf => sf.Key, sf => new { LostFlagIds = sf.AsEnumerable().Select(s => s.FlagId).Distinct() });

            var newSnapshots = teams.ToDictionary(t => t.Id, t => new ServiceStatsSnapshot()
            {
                TeamId = t.Id,
                RoundId = newLatestSnapshotRoundId,
                ServiceId = service.Id
            });

            var serviceStats = await _context.ServiceStats
                .Where(ss => ss.ServiceId == service.Id)
                .ToDictionaryAsync(ss => ss.TeamId);

            stopwatch.Stop();
            Logger.LogStatistics(CalculateServiceStatsFetchFinishedMessage.Create(roundId, service.Name, stopwatch.ElapsedMilliseconds));
            foreach (var team in teams)
            {
                double slaPoints = 0;
                double attackPoints = 0;
                double defPoints = 0;
                // SLA Points
                // snapshotted part
                if (oldSnapshots.ContainsKey(team.Id))
                {
                    slaPoints = oldSnapshots[team.Id].ServiceLevelAgreementPoints;
                    attackPoints = oldSnapshots[team.Id].AttackPoints;
                    defPoints = oldSnapshots[team.Id].LostDefensePoints;
                }
                else if (oldSnapshotsRoundId > 0) // old snapshould should have been here, but is not!
                {
                    Logger.LogWarning(new EnoLogMessage()
                    {
                        Message = "Snapshot is missing",
                        TeamName = team.Name,
                        ServiceName = service.Name,
                        RoundId = roundId,
                        Function = nameof(CalculateServiceStats),
                        Module = nameof(EnoDatabase)
                    });
                    var oldUps = await _context.RoundTeamServiceStates
                        .Where(rtss => rtss.GameRoundId <= oldSnapshotsRoundId)
                        .Where(rtss => rtss.ServiceId == service.Id)
                        .Where(rtss => rtss.TeamId == team.Id)
                        .Where(rtss => rtss.Status == ServiceStatus.Ok)
                        .CountAsync();

                    var oldRecoverings = await _context.RoundTeamServiceStates
                        .Where(rtss => rtss.GameRoundId <= oldSnapshotsRoundId)
                        .Where(rtss => rtss.ServiceId == service.Id)
                        .Where(rtss => rtss.TeamId == team.Id)
                        .Where(rtss => rtss.Status == ServiceStatus.Recovering)
                        .CountAsync();

                    slaPoints = (oldUps + (oldRecoverings / 2)) * Math.Sqrt(teams.Length);
                    //TODO do atk/def recalc too
                    throw new Exception("todo recalc atk/def");
                }

                // stable part
                double stableSLADiff = 0;
                if (stableServiceStates.TryGetValue(new { TeamId = team.Id, Status = ServiceStatus.Ok }, out var oks))
                {
                    stableSLADiff += oks.Amount;
                }
                if (stableServiceStates.TryGetValue(new { TeamId = team.Id, Status = ServiceStatus.Recovering }, out var recoverings))
                {
                    stableSLADiff += recoverings.Amount / 2;
                }
                slaPoints += stableSLADiff * Math.Sqrt(teams.Length);
                newSnapshots[team.Id].ServiceLevelAgreementPoints = slaPoints;

                // volatile part
                double volatileDiff = 0;
                if (volatileServiceStates.TryGetValue(new { TeamId = team.Id, Status = ServiceStatus.Ok }, out var volOks))
                {
                    volatileDiff += volOks.Amount;
                }
                if (volatileServiceStates.TryGetValue(new { TeamId = team.Id, Status = ServiceStatus.Recovering }, out var volRecoverings))
                {
                    volatileDiff += volRecoverings.Amount / 2;
                }
                slaPoints += volatileDiff * Math.Sqrt(teams.Length);
                serviceStats[team.Id].ServiceLevelAgreementPoints = slaPoints;

                // Attack Points
                // stable part
                if (stableCaptures.TryGetValue(team.Id, out var stableCapturesOfTeam))
                {
                    attackPoints += stableCapturesOfTeam.SubmittedFlags.Count();
                    foreach (var capture in stableCapturesOfTeam.SubmittedFlags)
                    {
                        if (allCapturesOfFlagsSinceSnapshot.TryGetValue(capture.FlagId, out var captures))
                        {
                            attackPoints += 1 / captures.Amount;
                        }
                        else
                        {
                            //TODO warning, ths should never happen
                        }
                    }
                }
                newSnapshots[team.Id].AttackPoints = attackPoints;

                // volatile part
                if (volatileCaptures.TryGetValue(team.Id, out var volatileCapturesOfTeam))
                {
                    attackPoints += volatileCapturesOfTeam.SubmittedFlags.Count();
                    foreach (var capture in volatileCapturesOfTeam.SubmittedFlags)
                    {
                        if (allCapturesOfFlagsSinceSnapshot.TryGetValue(capture.FlagId, out var captures))
                        {
                            attackPoints += 1 / captures.Amount;
                        }
                        else
                        {
                            //TODO warning, this should never happen
                        }
                    }
                }
                serviceStats[team.Id].AttackPoints = attackPoints;

                // Def Points
                // stable part
                if (stableLosses.TryGetValue(team.Id, out var stablelossesOfTeam))
                {
                    foreach (var loss in stablelossesOfTeam.LostFlagIds)
                    {
                        if (allCapturesOfFlagsSinceSnapshot.TryGetValue(loss, out var captures))
                        {
                            defPoints -= Math.Pow(captures.Amount, 0.75);
                        }
                        else
                        {
                            //TODO warning, this should never happen
                        }
                    }
                }
                newSnapshots[team.Id].LostDefensePoints = defPoints;

                // volatile part
                if (volatileLosses.TryGetValue(team.Id, out var volatileLossesOfTeam))
                {
                    foreach (var loss in volatileLossesOfTeam.LostFlagIds)
                    {
                        if (allCapturesOfFlagsSinceSnapshot.TryGetValue(loss, out var captures))
                        {
                            defPoints -= Math.Pow(captures.Amount, 0.75);
                        }
                        else
                        {
                            //TODO warning, this should never happen
                        }
                    }
                }
                serviceStats[team.Id].LostDefensePoints = defPoints;

                // service status
                newStates.TryGetValue((service.Id, team.Id), out var status_rtss);
                serviceStats[team.Id].Status = status_rtss?.Status ?? ServiceStatus.CheckerError;
            }
            if (newLatestSnapshotRoundId > 0)
            {
                _context.ServiceStatsSnapshots.AddRange(newSnapshots.Values);
            }
            await _context.SaveChangesAsync();
        }

        public EnoEngineScoreboard GetCurrentScoreboard(long roundId)
        {
            var teams = _context.Teams.AsNoTracking().OrderByDescending(t => t.TotalPoints).ToList();
            var round = _context.Rounds
                .AsNoTracking()
                .Where(r => r.Id == roundId)
                .FirstOrDefault();
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

        public async Task<long> GetTeamIdByPrefix(string attackerPrefixString)
        {
            return await _context.Teams
                .Where(t => t.TeamSubnet == attackerPrefixString)
                .Select(t => t.Id)
                .SingleAsync();
        }
    }
}
