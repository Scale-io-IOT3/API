#!/bin/sh

# Initialize DB if it doesn't exist
if [ ! -f /app/data/app.db ]; then
  echo "Initializing database..."
  mkdir -p /app/data
  sqlite3 /app/data/app.db < /app/data/init.sql
fi

# Decide command based on environment variable
if [ "$DOTNET_DEV" = "true" ]; then
  exec dotnet watch run --project API/API.csproj --urls http://0.0.0.0:8080
else
  exec dotnet API.dll
fi