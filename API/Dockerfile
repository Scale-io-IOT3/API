# ------------------------------
# Base runtime (prod)
# ------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS base
WORKDIR /app
EXPOSE 8080
# Npgsql 10 probes GSSAPI encryption when opening PostgreSQL connections.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

# ------------------------------
# Build stage
# ------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /src

COPY global.json ./
COPY API/API.csproj API/
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
RUN dotnet restore API/API.csproj

COPY . .
RUN dotnet publish API/API.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

# ------------------------------
# Dev runtime (SDK + optional watch)
# ------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS dev
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

COPY API/docker-dev.sh /usr/local/bin/api-watch
RUN chmod +x /usr/local/bin/api-watch
ENTRYPOINT ["/usr/local/bin/api-watch"]

# ------------------------------
# Final runtime (prod)
# ------------------------------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
CMD ["dotnet", "API.dll"]
