# Build context is the repo root (not this folder) — Nightfall.Api references sibling projects
# (Domain, Infrastructure), so the whole src/ tree needs to be in the build context.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first and restore, so this layer is cached unless a .csproj actually changes.
COPY src/Nightfall.Domain/Nightfall.Domain.csproj src/Nightfall.Domain/
COPY src/Nightfall.Infrastructure/Nightfall.Infrastructure.csproj src/Nightfall.Infrastructure/
COPY src/Nightfall.Api/Nightfall.Api.csproj src/Nightfall.Api/
RUN dotnet restore src/Nightfall.Api/Nightfall.Api.csproj

COPY src/Nightfall.Domain/ src/Nightfall.Domain/
COPY src/Nightfall.Infrastructure/ src/Nightfall.Infrastructure/
COPY src/Nightfall.Api/ src/Nightfall.Api/
RUN dotnet publish src/Nightfall.Api/Nightfall.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Nightfall.Api.dll"]
