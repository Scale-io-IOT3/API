# ------------------------------
# Base runtime (prod)
# ------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

RUN apt-get update && apt-get install -y sqlite3 libsqlite3-dev \
    && rm -rf /var/lib/apt/lists/*

# ------------------------------
# Build stage
# ------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY API/API.csproj API/
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
RUN dotnet restore API/API.csproj

COPY . .
RUN dotnet publish API/API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ------------------------------
# Final runtime (prod)
# ------------------------------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY ./Infrastructure/Persistence/init.sql /app/init.sql
ENTRYPOINT ["dotnet", "API.dll"]

# ------------------------------
# Dev runtime (SDK + watch)
# ------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dev
WORKDIR /app

RUN apt-get update && apt-get install -y sqlite3 libsqlite3-dev \
    && rm -rf /var/lib/apt/lists/*

COPY . .
RUN chmod +x /app/entrypoint.sh
ENTRYPOINT ["/app/entrypoint.sh"]
