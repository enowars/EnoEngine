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
using EnoCore.Logging;
using EnoCore.Models;

namespace EnoDatabase
{
    public enum FlagSubmissionResult
    {
        Ok,
        Invalid,
        Duplicate,
        Own,
        Old,
        UnknownError,
        InvalidSenderError,
        SpamError
    }

    public struct DBInitializationResult
    {
        public bool Success;
        public string ErrorMessage;
    }

    public interface IEnoDatabase
    {
        DBInitializationResult ApplyConfig(JsonConfiguration configuration);
        Task ProcessSubmissionsBatch(List<(Flag flag, long attackerTeamId, TaskCompletionSource<FlagSubmissionResult> result)> submissions, long flagValidityInRounds, EnoStatistics statistics);
        Task<(Round, Round, List<Flag>, List<Noise>, List<Havoc>)> CreateNewRound(DateTime begin, DateTime q2, DateTime q3, DateTime q4, DateTime end);
        Task CalculateRoundTeamServiceStates(IServiceProvider serviceProvider, long roundId, EnoStatistics statistics);
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
        Task<EnoEngineScoreboard> GetCurrentScoreboard(long roundId);
        void Migrate();
        Task UpdateTaskCheckerTaskResults(Memory<CheckerTask> tasks);
        Task<(long newLatestSnapshotRoundId, long oldSnapshotRoundId, Service[] services, Team[] teams)> GetPointCalculationFrame(long roundId, JsonConfiguration configuration);
        Task CalculateServiceStats(Team[] teams, long roundId, Service service, long oldSnapshotRoundId, long newLatestSnapshotRoundId);
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

        public DBInitializationResult ApplyConfig(JsonConfiguration config)
        {
            Logger.LogDebug("Applying configuration to database");
            if (config.RoundLengthInSeconds <= 0)
                return new DBInitializationResult
                {
                    Success = false,
                    ErrorMessage = $"RoundLengthInSeconds must be a positive integer, found {config.RoundLengthInSeconds}"
                };

            if (config.CheckedRoundsPerRound <= 0)
                return new DBInitializationResult
                {
                    Success = false,
                    ErrorMessage = $"CheckedRoundsPerRound must be a positive integer, found {config.CheckedRoundsPerRound}"
                };

            if (config.FlagValidityInRounds <= 0)
                return new DBInitializationResult
                {
                    Success = false,
                    ErrorMessage = $"CheckedRoundsPerRound must be a positive integer, found {config.FlagValidityInRounds}"
                };

            Migrate();
            return FillDatabase(config);
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

                string teamSubnet = EnoDatabaseUtils.ExtractSubnet(team.TeamSubnet, config.TeamSubnetBytesLength);

                // check if team is already present
                var dbTeam = _context.Teams
                    .Where(t => t.Id == team.Id)
                    .SingleOrDefault();
                if (dbTeam == null)
                {
                    Logger.LogInformation($"Adding team {team.Name}({team.Id})");
                    _context.Teams.Add(new Team()
                    {
                        TeamSubnet = teamSubnet,
                        Name = team.Name,
                        Id = team.Id,
                        Active = team.Active,
                        Address = team.Address
                    }) ;
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
                    Logger.LogInformation($"Adding service {service.Name}");
                    _context.Services.Add(new Service()
                    {
                        Id = service.Id,
                        Name = service.Name,
                        FlagsPerRound = service.FlagsPerRound,
                        NoisesPerRound = service.NoisesPerRound,
                        HavocsPerRound = service.HavocsPerRound,
                        FetchedFlagsPerRound = service.FetchedFlagsPerRound,
                        FetchedNoisesPerRound = service.FetchedNoisesPerRound,
                        FetchedHavocsPerRound = service.FetchedHavocsPerRound,
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
            foreach (var service in _context.Services.ToArray())
            {
                foreach (var team in _context.Teams.ToArray())
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
                            Status = ServiceStatus.OFFLINE
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
            Debug.WriteLine(flags[0]);
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
                            ServiceId = service.Id,
                            RoundOffset = i,
                            Round = round
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
                            StringRepresentation = EnoDatabaseUtils.GenerateNoise(),
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
            Logger.LogDebug($"InsertCheckerTasks inserting {tasks.Count()} tasks");
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
                .OrderByDescending(f => f.RoundId)
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
                .Where(f => f.RoundId == roundId)
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
                    Address = flag.Owner.Address ?? $"team{flag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.ToString(Encoding.ASCII.GetBytes(config.FlagSigningKey), config.Encoding),
                    RelatedRoundId = flag.RoundId,
                    CurrentRoundId = flag.RoundId,
                    StartTime = firstFlagTime,
                    TaskIndex = flag.RoundOffset,
                    Method = CheckerTaskMethod.putflag,
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
            tasks_start_time = EnoDatabaseUtils.Shuffle(tasks_start_time).ToList();
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
                    Address = noise.Owner.Address ?? $"team{noise.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = noise.StringRepresentation,
                    RelatedRoundId = noise.GameRoundId,
                    CurrentRoundId = noise.GameRoundId,
                    StartTime = firstFlagTime,
                    TaskIndex = noise.RoundOffset,
                    Method = CheckerTaskMethod.putnoise,
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
            tasks_start_time = EnoDatabaseUtils.Shuffle(tasks_start_time).ToList();
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
                    Address = havoc.Owner.Address ?? $"team{havoc.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = quarterRound,
                    RelatedRoundId = havoc.GameRoundId,
                    CurrentRoundId = roundId,
                    StartTime = begin,
                    TaskIndex = 0,
                    Method = CheckerTaskMethod.havoc,
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
            tasks_start_time = EnoDatabaseUtils.Shuffle(tasks_start_time).ToList();
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
                    Address = flag.Owner.Address ?? $"team{flag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = flag.ToString(Encoding.ASCII.GetBytes(config.FlagSigningKey), config.Encoding),
                    CurrentRoundId = flag.RoundId,
                    RelatedRoundId = flag.RoundId,
                    StartTime = q3,
                    TaskIndex = flag.RoundOffset,
                    Method = CheckerTaskMethod.getflag,
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
            tasks_start_time = EnoDatabaseUtils.Shuffle(tasks_start_time).ToList();
            tasks = tasks_start_time.Zip(tasks, (a, b) => { b.StartTime = a; return b; }).ToList();

            await InsertCheckerTasks(tasks);
        }

        public async Task InsertRetrieveOldFlagsTasks(Round currentRound, int oldRoundsCount, JsonConfiguration config)
        {
            int quarterRound = config.RoundLengthInSeconds / 4;
            var oldFlags = await _context.Flags
                .TagWith("InsertRetrieveOldFlagsTasks:oldFlags")
                .Where(f => f.RoundId  >= currentRound.Id - oldRoundsCount) // TODO skipped IDs
                .Where(f => f.RoundId != currentRound.Id)
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
                    Address = oldFlag.Owner.Address ?? $"team{oldFlag.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = quarterRound,
                    Payload = oldFlag.ToString(Encoding.ASCII.GetBytes(config.FlagSigningKey), config.Encoding),
                    RelatedRoundId = oldFlag.RoundId,
                    CurrentRoundId = currentRound.Id,
                    StartTime = time,
                    TaskIndex = oldFlag.RoundOffset,
                    Method = CheckerTaskMethod.getflag,
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
                    Address = noise.Owner.Address ?? $"team{noise.OwnerId}.{config.DnsSuffix}",
                    CheckerUrl = checkers[i % checkers.Length],
                    MaxRunningTime = maxRunningTime,
                    Payload = noise.StringRepresentation,
                    CurrentRoundId = noise.GameRoundId,
                    RelatedRoundId = noise.GameRoundId,
                    StartTime = q3,
                    TaskIndex = noise.RoundOffset,
                    Method = CheckerTaskMethod.getnoise,
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
            tasks_start_time = EnoDatabaseUtils.Shuffle(tasks_start_time).ToList();
            tasks = tasks_start_time.Zip(tasks, (a, b) => { b.StartTime = a; return b; }).ToList();

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
            foreach (var t in teams)
                foreach (var s in services)
            {
                    currentRoundWorstResults[(s.Id, t.Id)] =
                        await _context.CheckerTasks
                            .TagWith("CalculateRoundTeamServiceStates:currentRoundTasks")
                            .Where(ct => ct.CurrentRoundId == roundId)
                            .Where(ct => ct.RelatedRoundId == roundId)
                            .Where(ct => ct.TeamId == t.Id)
                            .Where(ct => ct.ServiceId == s.Id)
                            .Where(ct => ct.CheckerResult == CheckerResult.INTERNAL_ERROR)
                            .OrderBy(ct => ct.StartTime)
                            .Select(ct => new CheckerTask()
                            {
                                CheckerResult = ct.CheckerResult,
                                ErrorMessage = ct.ErrorMessage
                            })
                            .FirstOrDefaultAsync() ??
                        await _context.CheckerTasks
                            .TagWith("CalculateRoundTeamServiceStates:currentRoundTasks")
                            .Where(ct => ct.CurrentRoundId == roundId)
                            .Where(ct => ct.RelatedRoundId == roundId)
                            .Where(ct => ct.TeamId == t.Id)
                            .Where(ct => ct.ServiceId == s.Id)
                            .Where(ct => ct.CheckerResult == CheckerResult.OFFLINE)
                            .OrderBy(ct => ct.StartTime)
                            .Select(ct => new CheckerTask()
                            {
                                CheckerResult = ct.CheckerResult,
                                ErrorMessage = ct.ErrorMessage
                            })
                            .FirstOrDefaultAsync() ??
                        await _context.CheckerTasks
                            .TagWith("CalculateRoundTeamServiceStates:currentRoundTasks")
                            .Where(ct => ct.CurrentRoundId == roundId)
                            .Where(ct => ct.RelatedRoundId == roundId)
                            .Where(ct => ct.TeamId == t.Id)
                            .Where(ct => ct.ServiceId == s.Id)
                            .Where(ct => ct.CheckerResult == CheckerResult.MUMBLE)
                            .OrderBy(ct => ct.StartTime)
                            .Select(ct => new CheckerTask()
                            {
                                CheckerResult = ct.CheckerResult,
                                ErrorMessage = ct.ErrorMessage
                            })
                            .FirstOrDefaultAsync() ?? null;

            }
            sw.Stop();
            statistics.CheckerTaskFinishedMessage(roundId, "", sw.ElapsedMilliseconds);
            Logger.LogInformation($"CalculateRoundTeamServiceStates: Data Aggregation for {teams.Length} Teams and {services.Length} Services took {sw.ElapsedMilliseconds}ms");
            /*var currentRoundWorstResults = await _context.CheckerTasks
                .TagWith("CalculateRoundTeamServiceStates:currentRoundTasks")
                .Where(ct => ct.CurrentRoundId == roundId)
                .Where(ct => ct.RelatedRoundId == roundId)
                .Where (ct => ct.CheckerResult, ct => ct.ServiceId, ct => ct.TeamId */
            /*var currentRoundWorstResults = await _context.CheckerTasks
                .TagWith("CalculateRoundTeamServiceStates:currentRoundTasks")
                .Where(ct => ct.CurrentRoundId == roundId)
                .Where(ct => ct.RelatedRoundId == roundId)
                .GroupBy(ct => new { ct.ServiceId, ct.TeamId })
                .Select(g => g.Take(1).ToList())
                //.OrderBy(o => o.CheckerResult)
                //.Distinct()
                //.Select(g => new { key = g.Key, result = new CheckerTask{
                //    CheckerResult = g.Select(r=>r.CheckerResult).FirstOrDefault(),
                //    ErrorMessage = g.Select(r => r.ErrorMessage).FirstOrDefault()
                //} })
                //.OrderBy(s=> s.CheckerResult).Take(1) })
                //.Select(g => new { g.Key, WorstResult = g.Min(ct => ct.CheckerResult) })
                .AsNoTracking()
                .ToListAsync();
                //.ToDictionaryAsync(g => g.key, g => g.result);  */

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
                    var key2 = (service.Id, team.Id);
                    var key = new { ServiceId = service.Id, TeamId = team.Id };
                    ServiceStatus status = ServiceStatus.INTERNAL_ERROR;
                    string? message = null;
                    if (currentRoundWorstResults.ContainsKey(key2))
                    {
                        if (currentRoundWorstResults[key2] != null)
                        {
                            status = EnoDatabaseUtils.CheckerResultToServiceStatus(currentRoundWorstResults[key2]!.CheckerResult);
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
                    states[(key.ServiceId, key.TeamId)] = new RoundTeamServiceState()
                    {
                        GameRoundId = roundId,
                        ServiceId = key.ServiceId,
                        TeamId = key.TeamId,
                        Status = status,
                        ErrorMessage = message
                    };
                }
            }
            _context.RoundTeamServiceStates.AddRange(states.Values);
            await _context.SaveChangesAsync();
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

        public async Task<Round> PrepareRecalculation()
        {
            await _context.Database.ExecuteSqlRawAsync($"delete from \"{nameof(_context.ServiceStatsSnapshots)}\";");
            return await _context.Rounds
                .OrderByDescending(r => r.Id)
                .Skip(1)
                .FirstOrDefaultAsync();
        }
    }
}
