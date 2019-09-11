# EnoEngine
[![Build Status](https://dev.azure.com/ENOFLAG/ENOWARS/_apis/build/status/enowars.EnoEngine?branchName=master)](https://dev.azure.com/ENOFLAG/ENOWARS/_build/latest?definitionId=1&branchName=master)
![](https://tokei.rs/b1/github/enowars/EnoEngine)

This is the engine powering our CTFs.

For random reasons, it's written in C#.

## Development

Develop either in Visual Studio, or, for the FOSS people, for Visual Studio Code

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
    team: string;
    relatedRoundId: number;
    round: number;
    flag: string | null;
    flagIndex: number | null;
}
```
Response:
```ts
interface CheckerResultMessage {
    result: string; // "INTERNAL_ERROR", "OK", MUMBLE", or "OFFLINE"
}
```
