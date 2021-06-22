# EnoEngine [![Build Status](https://github.com/enowars/EnoEngine/actions/workflows/ci.yml/badge.svg)](https://github.com/enowars/EnoEngine/actions/workflows/ci.yml) ![](https://tokei.rs/b1/github/enowars/EnoEngine) ![](https://img.shields.io/nuget/v/enocore)

This is the engine powering our CTFs.

For performance reasons, it's written in C#.

## Usage

(0. Make sure the dependencies are installed: docker, docker-compose, dotnet sdk ...)

1. Create a ctf.json (see below)
2. Make sure the data folder exists (./../data/)
3. Start up the Database (`docker-compose up -d`) (or run `tmux.sh`)
4. Run EnoLauncher (`dotnet run -c Release -p EnoLauncher`)
5. Run EnoFlagSink (`dotnet run -c Release -p EnoFlagSink`)
6. Once you want to start the CTF (i.e. distribute flags): run EnoEngine (`dotnet run -c Release -p EnoEngine`)

## ctf.json Format

You can find the schema under [`/ctf.schema.json`](./ctf.schema.json). To reference it in your json use the following snippet:

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/enowars/EnoEngine/master/ctf.schema.json",
  "title": "Your CTF Name"
  // ...
}
```

> To generate the newest Schema execute `dotnet run -c Release -p EnoCore`.

## Development

1. Install the dotnet sdk-5. [Download](https://dotnet.microsoft.com/download/visual-studio-sdks)
2. Use any IDE you like (Visual Studio or VSCode recommended)
3. If your IDE doesn't do it automatically, run `dotnet restore`


## Database

For creating a migration after changes, run this:

```
cd ./EnoDatabase
rm -r Migrations
dotnet ef migrations add InitialMigrations --startup-project ../EnoEngine
```

## Checker API v2
Checkers are expected to respond to these requests, providing a HTTP Status Code 200:

### `GET /service`
Response:
```ts
interface CheckerInfoMessage {
    serviceName: string;                        // Name of the service
    flagVariants: number;                       // Number of different variants supported for storing/retrieving flags. Each variant must correspond to a different location/flag store in the service.
    noiseVariants: number;                      // Number of different variants supported for storing/retrieving noise. Different variants must not necessarily store the noise in different locations.
    havocVariants: number;                      // Number of different variants supported for havoc.
}
```

### `POST /`
Parameter:
```ts
interface CheckerTaskMessage {
    taskId: number;                             // The per-ctf unique id of a task.
    method: "putflag" | "getflag" | "putnoise" | "getnoise" | "havoc";
    address: string;                            // The address of the target team's vulnbox. Can be either an IP address or a valid hostname.
    teamId: number;                             // The id of the target team.
    teamName: string;                           // The name of the target team.
    currentRoundId: number;                     // The id of the current round.
    relatedRoundId: number;                     // For "getflag" and "getnoise", this is the id of the round in which the corresponding "putflag" or "putnoise" happened. For "putflag", "putnoise" and "havoc", this is always identical to currentRoundId. Use the taskChainId to store/retrieve data related to the corresponding "putflag" or "putnoise" instead of using relatedRoundId directly.
    flag: string | null;                        // The flag for putflag and getflag, otherwise null.
    variantId: number;                          // The variant id of the task. Used to support different flag, noise and havoc methods. Starts at 0.
    timeout: number;                            // Timeout for the task in milliseconds.
    roundLength: number;                        // Round length in milliseconds.
    taskChainId: string;                        // The unique identifier of a chain of tasks (i.e. putflag and getflags or putnoise and getnoise for the same flag/noise share an Id, each havoc has its own Id). Should be used in the database to store e.g. credentials created during putlfag and required in getflag. It is up to the caller to ensure the aforementioned criteria are met, the Engine achieves this by composing it the following way: "{flag|noise|havoc}_s{serviceId}_r{relatedRoundId}_t{teamId}_i{uniqueVariantIndex}". A checker may be called multiple times with the same method, serviceId, roundId, teamId and variantId, in which case the uniqueVariantIndex can be used to distinguish the taskChains.
}
```
Response:
```ts
interface CheckerResultMessage {
    result: string;                             // "INTERNAL_ERROR", "OK", MUMBLE", or "OFFLINE".
    message: string | null;                     // message describing the error, displayed on the public scoreboard if not null
}
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