#!/bin/sh
set -e

# Initialize DB if it doesn't exist
if [ ! -f /app/data/app.db ]; then
  echo "Initializing database..."
  mkdir -p /app/data
  sqlite3 /app/data/app.db < /app/data/init.sql
fi

exec "$@"