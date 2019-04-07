FROM microsoft/dotnet:2.2-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY EnoLauncher/EnoLauncher.csproj EnoLauncher/
COPY EnoCore/EnoCore.csproj EnoCore/
RUN dotnet restore EnoLauncher/EnoLauncher.csproj
COPY . .
WORKDIR /src/EnoLauncher
RUN dotnet build EnoLauncher.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish EnoLauncher.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "EnoLauncher.dll"]
