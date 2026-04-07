# ------------------------------
# Base runtime (prod)
# ------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

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
ENTRYPOINT ["dotnet", "API.dll"]

# ------------------------------
# Dev runtime (SDK + watch)
# ------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dev
WORKDIR /app

COPY . .
ENTRYPOINT ["dotnet", "watch", "--non-interactive", "--project", "API/API.csproj", "run", "--urls", "http://0.0.0.0:8080"]
