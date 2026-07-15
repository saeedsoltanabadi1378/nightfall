# Build context is the repo root — see src/Nightfall.Api/Dockerfile for why.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Nightfall.Domain/Nightfall.Domain.csproj src/Nightfall.Domain/
COPY src/Nightfall.Infrastructure/Nightfall.Infrastructure.csproj src/Nightfall.Infrastructure/
COPY src/Nightfall.Bot/Nightfall.Bot.csproj src/Nightfall.Bot/
RUN dotnet restore src/Nightfall.Bot/Nightfall.Bot.csproj

COPY src/Nightfall.Domain/ src/Nightfall.Domain/
COPY src/Nightfall.Infrastructure/ src/Nightfall.Infrastructure/
COPY src/Nightfall.Bot/ src/Nightfall.Bot/
RUN dotnet publish src/Nightfall.Bot/Nightfall.Bot.csproj -c Release -o /app/publish --no-restore

# Worker service, no HTTP hosting needed — plain "runtime" image, not "aspnet".
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Nightfall.Bot.dll"]
