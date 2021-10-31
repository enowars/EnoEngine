FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

WORKDIR /stresstest
COPY . .

RUN dotnet build -c Release
RUN mkdir ../data
COPY EnoFlagSink/StressTest/ctf.json .

ENV DATABASE_DOMAIN enopostgres

ENTRYPOINT dotnet run -c Release --project EnoFlagSink
