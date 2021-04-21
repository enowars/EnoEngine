# EnoEngine [![Build Status](https://dev.azure.com/ENOFLAG/ENOWARS/_apis/build/status/enowars.EnoEngine?branchName=master)](https://dev.azure.com/ENOFLAG/ENOWARS/_build) ![](https://tokei.rs/b1/github/enowars/EnoEngine)

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
```ts
interface ctfjson {
    title: string;
    flagValidityInRounds: number;
    checkedRoundsPerRound: number;
    roundLengthInSeconds: number;
    dnsSuffix: string;
    teamSubnetBytesLength: number;
    flagSigningKey: string;
    encoding: string | null;
    services: Service[];
    teams: Team[];
}
interface Service {
    id: number;
    name: string;
    flagsPerRoundMultiplier: number;
    noisesPerRoundMultiplier: number;
    havocsPerRoundMultiplier: number;
    weightFactor: number;
    active: string | null;
    checkers: string[];
}

interface Team {
    id: number;
    name: string;
    address: string | null;
    teamSubnet: string;
    logoUrl: string | null;
    countryCode: string | null;
    active: string | null;
}
```

## Development
- Install the dotnet sdk
- Use any IDE you like
- If your IDE doesn't do it automatically, run `dotnet restore`

## Database
For creating a migration after changes, run this:
```
cd ./EnoDatabase
rm -r Migrations
dotnet ef migrations add InitialMigrations --startup-project ../EnoEngine
```

## Checker API
Checkers are expected to respond to these requests, providing a HTTP Status Code 200:

### `GET /service`
Response:
```ts
interface CheckerInfoMessage {
    serviceName: string;
    flagCount: number;
    havocCount: number;
    noiseCount: number;
}
```

### `POST /`
Parameter:
```ts
interface CheckerTaskMessage {
    taskId: number;                             // The per-ctf unique id of a task.
    method: string;                             // "putflag", "getflag", "putnoise", "getnoise" or "havoc".
    address: string;                            // The address of the target team's vulnbox. Can be either an IP address or a valid hostname.
    teamId: number;                             // The id of the target team.
    teamName: string;                           // The name of the target team.
    currentRoundId: number;                     // The id of the current round.
    relatedRoundId: number;                     // The id of the round in which the "putflag", "putnoise" or "havoc" happened.
    flag: string | null;                        // The flag for putflag and getflag, otherwise null.
    variantId: number;                          // The variant id of the task. Used to support different flag and noise methods.
    timeout: number;                            // Timeout in milliseconds.
    roundLength: number;                        // Round length in milliseconds.
    taskContextId: string;                      // The unique identifier of a set of related tasks (i.e. putflag and its getflags, and putnoise and its getnoises, and individual havocs.). Always composed in the following way: "{flag|noise|havoc_s{serviceId}_r{roundId}_t{teamId}_i{index}", and should be used as your database index.
}
```
Response:
```ts
interface CheckerResultMessage {
    result: string;                             // "INTERNAL_ERROR", "OK", MUMBLE", or "OFFLINE".
    message: string | null;
}
```

## Scoreboard API
```ts
interface ScoreboardInfo {
    dnsSuffix: string | null;                   // The DNS suffix (including the leading dot), if DNS is used. Example: ".bambi.ovh"
    services: Service[];
    teams: ScoreboardInfoTeam[];
}

interface ScoreboardInfoTeam {
    id: number;                                 // The id of the team.
    name: string;                               // The name of the team.
    logoUrl: string | null;                     // An URL with the team's logo, or null.
    countryCode: string | null;                 // The ISO 3166-1 alpha-2 country code (uppercase), or null.
}

interface Scoreboard {
    currentRoundId: number | null;
    startTimestamp: string | null;              // Start timestamp of the current round according to ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ") in UTC.
    endTimestamp: string | null;                // End timestamp of the current round according to ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ") in UTC.
    dnsSuffix: string | null;                   // The DNS suffix (including the leading dot), if DNS is used. Example: ".bambi.ovh"
    services: Service[];
    teams: Team[];
}

interface Team {
    teamName: string;                           // The name of the team.
    teamId: number;                             // The id of the team.
    totalPoints: number;                        // The total points of the team.
    attackPoints: number;                       // The attack points of the team.
    defensePoints: number;                      // The defense points of the team.
    serviceLevelAgreementPoints: number;        // The SLA points of the team.
    serviceDetails: ServiceDetail[];
}

interface ServiceDetail {
    serviceId: number;                          // The id of the service.
    attackPoints: number;                       // The attack points of the team in the service.
    defensePoints: number;                      // The defense points of the team.
    serviceLevelAgreementPoints: number;        // The SLA points of the team in the service.
    serviceStatus: string;                      // "INTERNAL_ERROR", "OFFLINE", "MUMBLE", "RECOVERING", "OK", "INACTIVE"
    message: string | null;                     // Leave null for no message, otherwise the message is displayed
}

interface Service {
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
