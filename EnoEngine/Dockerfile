FROM microsoft/dotnet:2.2-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY EnoEngine/EnoEngine.csproj EnoEngine/
COPY EnoCore/EnoCore.csproj EnoCore/
RUN dotnet restore EnoEngine/EnoEngine.csproj
COPY . .
WORKDIR /src/EnoEngine
RUN dotnet build EnoEngine.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish EnoEngine.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "EnoEngine.dll"]
