# ------------------------------
# Base runtime (prod)
# ------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
# Npgsql 10 probes GSSAPI encryption when opening PostgreSQL connections.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

# ------------------------------
# Build stage
# ------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
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
USER $APP_UID
CMD ["dotnet", "API.dll"]

# ------------------------------
# Dev runtime (SDK + watch)
# ------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

COPY . .
ENTRYPOINT ["dotnet", "watch", "--non-interactive", "--project", "API/API.csproj", "run", "--urls", "http://0.0.0.0:8080"]
