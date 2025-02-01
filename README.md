# EnoEngine [![Build Status](https://github.com/enowars/EnoEngine/actions/workflows/ci.yml/badge.svg)](https://github.com/enowars/EnoEngine/actions/workflows/ci.yml) ![](https://tokei.rs/b1/github/enowars/EnoEngine) ![](https://img.shields.io/nuget/v/enocore)

This is the engine powering our CTFs.

For performance reasons, it's written in C#.

## Usage
(0. Make sure the dependencies are installed: docker, docker-compose, dotnet sdk ...)
1. Create a ctf.json (see below)
2. Make sure the data folder exists (./../data/)
3. Start up the Database (`docker-compose up -d`) (or run `tmux.sh`)
4. Run EnoConfig to apply the configuration (`dotnet run --project EnoConfig apply`)
5. Run EnoLauncher (`dotnet run -c Release --project EnoLauncher`)
6. Run EnoFlagSink (`dotnet run -c Release --project EnoFlagSink`)
6. Run EnoScoring (`dotnet run -c Release --project EnoScoring`)
7. Once you want to start the CTF (i.e. distribute flags): run EnoEngine (`dotnet run -c Release --project EnoEngine`)


## Database
For creating a migration after changes, run this:
```
cd ./EnoDatabase
rm -r Migrations
dotnet ef migrations add Mfoo
```


## Scoreboard API
```ts
interface Scoreboard {
    currentRoundId: number | null;
    startTimestamp: string | null;              // Start timestamp of the current round according to ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ") in UTC.
    endTimestamp: string | null;                // End timestamp of the current round according to ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ") in UTC.
    dnsSuffix: string | null;                   // The DNS suffix (including the leading dot), if DNS is used. Example: ".bambi.ovh"
    services: ScoreboardService[];
    teams: ScoreboardTeam[];
}

interface ScoreboardTeam {
    teamName: string;                           // The name of the team.
    teamId: number;                             // The id of the team.
    logoUrl: string | null;                     // An URL with the team's logo, or null.
    countryCode: string | null;                 // The ISO 3166-1 alpha-2 country code (uppercase), or null.
    totalScore: number;                         // The total Score of the team.
    attackScore: number;                        // The attack Score of the team.
    defenseScore: number;                       // The defense Score of the team.
    serviceLevelAgreementScore: number;         // The SLA Score of the team.
    serviceDetails: ScoreboardTeamServiceDetails[];
}

interface ScoreboardTeamServiceDetails {
    serviceId: number;                          // The id of the service.
    attackScore: number;                        // The attack Score of the team in the service.
    defenseScore: number;                       // The defense Score of the team.
    serviceLevelAgreementScore: number;         // The SLA Score of the team in the service.
    serviceStatus: string;                      // "INTERNAL_ERROR", "OFFLINE", "MUMBLE", "RECOVERING", "OK", "INACTIVE"
    message: string | null;                     // Leave null for no message, otherwise the message is displayed
}

interface ScoreboardService {
    serviceId: number;                          // The id of the service.
    serviceName: string;                        // The name of the service.
    flagVariants: number;                       // The amount of different flag variants.
    firstBloods: FirstBlood[];
}

interface FirstBlood {
    teamId: number;                             // The id of the team that scored the firstblood.
    teamName: number;                           // The name of the team that scored the firstblood.
    timestamp: string;                          // Timestamp according to ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ") in UTC.
    roundId: number;                            // The id of the round in which the firstblood was submitted.
    flagVariantId: number;                      // The id of the variant.
}
```
## Flagsubmission Endpoint:
The Flagsubmission is done via a tcp connection to port 1337. There you can just send the flag or multiple flags delimited by \n characters. Each Flag will be checked by the backend and then a message is returned picked by this code:
```
    FlagSubmissionResult.Ok => "VALID: Flag accepted!\n",
    FlagSubmissionResult.Invalid => "INVALID: You have submitted an invalid string!\n",
    FlagSubmissionResult.Duplicate => "RESUBMIT: You have already sent this flag!\n",
    FlagSubmissionResult.Own => "OWNFLAG: This flag belongs to you!\n",
    FlagSubmissionResult.Old => "OLD: You have submitted an old flag!\n",
    FlagSubmissionResult.UnknownError => "ERROR: An unexpected error occured :(\n",
    FlagSubmissionResult.InvalidSenderError => "ILLEGAL: Your IP address does not belong to any team's subnet!\n",
    FlagSubmissionResult.SpamError => "SPAM: You should send 1 flag per line!\n",
```
