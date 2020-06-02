# EnoEngine [![Build Status](https://dev.azure.com/ENOFLAG/ENOWARS/_apis/build/status/enowars.EnoEngine?branchName=master)](https://dev.azure.com/ENOFLAG/ENOWARS/_build/latest?definitionId=1&branchName=master) ![](https://tokei.rs/b1/github/enowars/EnoEngine)

This is the engine powering our CTFs.

For random reasons, it's written in C#.

## Usage

1. Create a ctf.json (see examples)
2. Run EnoLauncher (`dotnet run --project EnoLauncher`)
3. Run EnoEngine (`dotnet run --project EnoEngine`)

## Development

Develop either in Visual Studio, or, for the FOSS people, in Visual Studio Code

- install dotnet core (on Windows, `choco install dotnetcore-sdk`)
- run `dotnet restore`
- open folder in VS Code

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
    serviceId: string;
    serviceName: string;
    teamId: string;
    teamName: string;
    relatedRoundId: number;
    roundId: number;
    flag: string | null;
    flagIndex: number;
}
```
Response:
```ts
interface CheckerResultMessage {
    result: string; // "INTERNAL_ERROR", "OK", MUMBLE", or "OFFLINE"
}
```
