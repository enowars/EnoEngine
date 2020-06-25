# EnoEngine [![Build Status](https://dev.azure.com/ENOFLAG/ENOWARS/_apis/build/status/enowars.EnoEngine?branchName=master)](https://dev.azure.com/ENOFLAG/ENOWARS/_build) ![](https://tokei.rs/b1/github/enowars/EnoEngine)

This is the engine powering our CTFs.

For performance reasons, it's written in C#.

## Usage
(0. Make sure the dependencies are installed: docker, docker-compose, dotnet sdk ...)
1. Create a ctf.json (see below)
2. Make sure the data folder exists (./../data/)
3. Start up the Database (`docker-compose up -d`)
4. Run EnoLauncher (`dotnet run --project EnoLauncher`)
5. Run EnoEngine (`dotnet run --project EnoEngine`)

## Development

Develop either in Visual Studio, or, for the FOSS people, in Visual Studio Code:

- install dotnet core (on Windows, `choco install dotnetcore-sdk`)
- run `dotnet restore`
- open folder in VS Code

## Database
For creating a migration after changes, run this:
```
cd /EnoDatabase
rm -r Migrations
dotnet ef migrations add InitialMigrations --startup-project ../EnoEngine
```
## ctf.json Format
```ts
interface ctfjson {
    Title: string;
    FlagValidityInRounds: number;
    CheckedRoundsPerRound: number;
    RoundLengthInSeconds: number;
    DnsSuffix: string;
    TeamSubnetBytesLength: number;
    FlagSigningKey: string;
    NoiseSigningKey: string;
    Encoding: string | null;
    Services: Service[];
    Teams: Team[];
}
interface Service {
    Id: number;
    Name: string;
    FlagsPerRound: number;
    NoisesPerRound: number;
    HavocsPerRound: number;
    WeightFactor: number;
    Active: string | null;
    Checkers: string[];
}

interface Team {
    Id: number;
    Name: string;
    Address: string | null;
    TeamSubnet: string;
    LogoUrl: string | null;
    FlagUrl: string | null;
}
```

## Checker API

Checkers are expected to respond to these requests:

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
}
```
Response:
```ts
interface CheckerResultMessage {
    result: string;                             // "INTERNAL_ERROR", "OK", MUMBLE", or "OFFLINE"
}
```

## Scoreboard API
```ts
interface ScoreboardInfo {
    CurrentRound: number | null;
    StartTimestamp: string | null;              // Timestamps according ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ")
    StartTimeEpoch: number | null;              // Unix time in seconds
    EndTimestamp: string | null;                // Timestamps according ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ")
    EndTimeEpoch: number | null;                // Unix time in seconds
    Services: Service[];
    Teams: Team[];
}

interface Team {
    TeamName: string;                               //"teamname40"
    TeamId: number;                             //40
    TotalPoints: number;                        //2692.662622758371
    AttackPoints: number;                       //0.0
    LostDefensePoints: number;                  //0.0
    ServiceLevelAgreementPoints: number;        //2692.662622758371
    ServiceDetails: ServiceDetail[];
}

interface ServiceDetail {
    ServiceId: number;
    AttackPoints: number;
    LostDefensePoints: number;
    ServiceLevelAgreementPoints: number;
    ServiceStatus: string;                      // INTERNAL_ERROR,OFFLINE,MUMBLE,RECOVERING,OK,INACTIVE
    Message: string | null;                     // Leave null for no message, otherwise the message is displayed
}

interface Service {
    ServiceId: number;
    ServiceName: string;
    MaxStores: number;
    FirstBloods: FirstBlood[];
}

interface FirstBlood {
    TeamId: number;
    Timestamp: string;                  // Timestamps according ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ")
    TimeEpoch: number;                  // Unix time in seconds
    RoundId: number;
    StoreDescription: string | null;
    StoreIndex: number;

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
