FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
RUN apt-get update && apt-get install -y --no-install-recommends netcat nano less tcpdump

WORKDIR /stresstest
COPY . .

RUN dotnet build -c Release
RUN mkdir ../data
COPY EnoFlagSink/StressTest/ctf.json .

ENV DATABASE_DOMAIN enopostgres

ENTRYPOINT EnoFlagSink/StressTest/submit.sh
