#!/usr/bin/env bash
set -euo pipefail

production_image=${1:-scaleio-production:check}
development_image=${2:-scaleio-development:check}
prefix="scaleio-smoke-$$"
root=$(cd "$(dirname "$0")/.." && pwd)
cleanup() {
  result=$?
  if [[ "$result" != 0 ]]; then
    docker logs "$prefix-production" 2>/dev/null || true
    docker logs "$prefix-development" 2>/dev/null || true
  fi
  docker rm -fv "$prefix-production" "$prefix-development" "$prefix-db" >/dev/null 2>&1 || true
  docker network rm "$prefix" >/dev/null 2>&1 || true
  exit "$result"
}
trap cleanup EXIT

postgres_image=$(docker compose --project-directory "$root" -f "$root/docker-compose.yml" config --format json |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["services"]["postgres"]["image"])')
docker network create "$prefix" >/dev/null
docker run -d --name "$prefix-db" --network "$prefix" --network-alias postgres \
  -e POSTGRES_DB=scaleio -e POSTGRES_PASSWORD=postgres \
  "$postgres_image" >/dev/null
ready=false
for ((i=0; i<60; i++)); do
  if docker exec "$prefix-db" pg_isready -U postgres -d scaleio >/dev/null 2>&1; then ready=true; break; fi
  sleep 1
done
[[ "$ready" == true ]]

common=(--network "$prefix"
  -e 'DATABASE_URL=Host=postgres;Port=5432;Database=scaleio;Username=postgres;Password=postgres'
  -e ApplyMigrationsOnStartup=true -e SeedDefaultUserOnStartup=false
  -e Jwt__Issuer=http://localhost -e Jwt__Audience=http://localhost
  -e Jwt__Key=c2NhbGVpby1sb2NhbC1kZXZlbG9wbWVudC1vbmx5LWtleS0zMg==
  -e Jwt__TokenValidityMins=60)

docker run -d --name "$prefix-production" "${common[@]}" \
  -e ASPNETCORE_ENVIRONMENT=Production -p 127.0.0.1::8080 "$production_image" >/dev/null
endpoint=$(docker port "$prefix-production" 8080/tcp)
ready=false
for ((i=0; i<120; i++)); do
  if curl -fsS --max-time 2 "http://$endpoint/health" >/dev/null 2>&1; then ready=true; break; fi
  sleep 1
done
[[ "$ready" == true ]]
[[ "$(docker exec "$prefix-production" id -u)" != 0 ]]
docker stop --time 30 "$prefix-production" >/dev/null
[[ "$(docker inspect -f '{{.State.ExitCode}}' "$prefix-production")" == 0 ]]

mounts=(-v "$root:/app:ro" -v /root/.nuget/packages)
for project in API Core Infrastructure Tests; do
  mounts+=(-v "/app/$project/bin" -v "/app/$project/obj")
done
docker run -d --init --name "$prefix-development" "${common[@]}" "${mounts[@]}" \
  -e ASPNETCORE_ENVIRONMENT=Development -e API_WATCH_MODE=off \
  -e DOTNET_USE_POLLING_FILE_WATCHER=1 "$development_image" >/dev/null
docker exec "$prefix-development" api-watch off
docker exec "$prefix-development" api-watch on
docker exec "$prefix-development" api-watch status
docker exec "$prefix-development" api-watch off
docker stop --time 30 "$prefix-development" >/dev/null
[[ "$(docker inspect -f '{{.State.ExitCode}}' "$prefix-development")" == 0 ]]
echo 'Production readiness/non-root/shutdown and development watch switching/shutdown passed.'
