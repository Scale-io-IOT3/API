# ------------------------------
# Base image
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

COPY ["API/API.csproj", "Core/Core.csproj", "Infrastructure/Infrastructure.csproj", "./"]
RUN dotnet restore "API/API.csproj"

COPY . .
RUN dotnet publish "API/API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ------------------------------
# Production runtime
# ------------------------------
FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .
COPY ./Infrastructure/Persistence/init.sql /app/init.sql
COPY entrypoint.sh /app/entrypoint.sh

RUN chmod +x /app/entrypoint.sh
VOLUME /app/data
ENTRYPOINT ["/app/entrypoint.sh"]

# ------------------------------
# Development runtime
# ------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dev
ENV DOTNET_DEV=true
WORKDIR /app

RUN apt-get update && apt-get install -y sqlite3 libsqlite3-dev \
    && rm -rf /var/lib/apt/lists/*

COPY . .
COPY entrypoint.sh /app/entrypoint.sh

RUN chmod +x /app/entrypoint.sh
ENTRYPOINT ["/app/entrypoint.sh"]
