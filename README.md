# EnoEngine [![Build Status](https://dev.azure.com/ENOFLAG/ENOWARS/_apis/build/status/enowars.EnoEngine?branchName=master)](https://dev.azure.com/ENOFLAG/ENOWARS/_build) ![](https://tokei.rs/b1/github/enowars/EnoEngine)

This is the engine powering our CTFs.

For performance reasons, it's written in C#.

## Usage
(0. Make sure the dependencies are installed: docker, docker-compose, dotnet sdk ...)
1. Create a ctf.json (see below)
2. Make sure the data folder exists (./../data/)
3. Start up the Database (`docker-compose up -d`)
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
    countryFlagUrl: string | null;
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
    runId: number;
    method: string;
    address: string;
    serviceId: number;
    serviceName: string;
    teamId: number;
    teamName: string;
    roundId: number;
    relatedRoundId: number;
    flag: string | null;
    flagIndex: number;
    timeout: number;                            // Timeout in miliseconds
    roundLength: number;                        // Round Length in seconds
}
```
Response:
```ts
interface CheckerResultMessage {
    result: string;                             // "INTERNAL_ERROR", "OK", MUMBLE", or "OFFLINE"
    message: string | null;
}
```

## Scoreboard API
```ts
interface ScoreboardInfo {
    dnsSuffix: string | null;                   // ".bambi.ovh"
    services: Service[];
    teams: ScoreboardInfoTeam[];
}

interface ScoreboardInfoTeam {
    id: number;                                 // 40
    name: string;                               // "teamname40"
    logoUrl: string | null;                     // "http://..."
    flagUrl: string | null;                     // "http://..."
}

interface Scoreboard {
    currentRound: number | null;
    startTimestamp: string | null;              // Timestamps according ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ")
    startTimeEpoch: number | null;              // Unix time in seconds
    endTimestamp: string | null;                // Timestamps according ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ")
    endTimeEpoch: number | null;                // Unix time in seconds
    dnsSuffix: string | null;                   // ".bambi.ovh"
    services: Service[];
    teams: Team[];
}

interface Team {
    teamName: string;                           // "teamname40"
    teamId: number;                             // 40
    totalPoints: number;                        // 2692.662622758371
    attackPoints: number;                       // 0.0
    lostDefensePoints: number;                  // 0.0
    serviceLevelAgreementPoints: number;        // 2692.662622758371
    serviceDetails: ServiceDetail[];
}

interface ServiceDetail {
    serviceId: number;                          // 0.0
    attackPoints: number;                       // 0.0
    lostDefensePoints: number;                  // 0.0
    serviceLevelAgreementPoints: number;        // 0.0
    serviceStatus: string;                      // INTERNAL_ERROR,OFFLINE,MUMBLE,RECOVERING,OK,INACTIVE
    message: string | null;                     // Leave null for no message, otherwise the message is displayed
}

interface Service {
    serviceId: number;
    serviceName: string;
    maxStores: number;
    firstBloods: FirstBlood[];
}

interface FirstBlood {
    teamId: number;
    timestamp: string;                          // Timestamps according ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ")
    timeEpoch: number;                          // Unix time in seconds
    roundId: number;                            // 1
    storeDescription: string | null;            // "Private user notes"
    storeIndex: number;                         // 0
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
