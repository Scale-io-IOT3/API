#!/usr/bin/env bash
set -eu

mode_file=/tmp/scaleio-watch-mode
active_file=/tmp/scaleio-watch-active

case "${1:-}" in
  on|off)
    mode_tmp=$(mktemp "${mode_file}.XXXXXX")
    printf '%s\n' "$1" > "$mode_tmp"
    mv "$mode_tmp" "$mode_file"
    for ((attempt=0; attempt<300; attempt++)); do
      if [[ "$(cat "$active_file" 2>/dev/null || true)" == "$1" ]] &&
         curl -fsS --max-time 1 http://localhost:8080/health >/dev/null 2>&1; then
        echo "Watch mode: $1; API is ready."
        exit 0
      fi
      sleep 1
    done
    echo "Watch mode requested: $1; API readiness timed out. Check container logs." >&2
    exit 1
    ;;
  status)
    echo "Requested: $(cat "$mode_file")"
    echo "Active: $(cat "$active_file" 2>/dev/null || echo stopped)"
    if curl -fsS --max-time 2 http://localhost:8080/health >/dev/null 2>&1; then
      echo "API: ready"
    else
      echo "API: unavailable"
      exit 1
    fi
    exit 0
    ;;
  "") ;;
  *) echo "Usage: api-watch [on|off|status]" >&2; exit 2 ;;
esac

# Job control gives dotnet and its descendants their own process group.
set -m
child=
stop_api() {
  rm -f "$active_file"
  if [[ -n "$child" ]]; then
    kill -TERM -- "-$child" 2>/dev/null || true
    for ((i=0; i<10; i++)); do
      kill -0 -- "-$child" 2>/dev/null || break
      sleep 1
    done
    kill -KILL -- "-$child" 2>/dev/null || true
    wait "$child" 2>/dev/null || true
    child=
  fi
}
trap 'stop_api; exit 0' TERM INT
trap stop_api EXIT

mode=${API_WATCH_MODE:-off}
case "$mode" in on|off) ;; *) echo "API_WATCH_MODE must be on or off" >&2; exit 2 ;; esac
printf '%s\n' "$mode" > "$mode_file"

while true; do
  echo "Starting API with watch mode: $mode"
  if [[ "$mode" == on ]]; then
    dotnet watch --non-interactive --project API/API.csproj run --no-launch-profile --urls http://0.0.0.0:8080 &
  else
    dotnet run --project API/API.csproj --no-launch-profile --urls http://0.0.0.0:8080 &
  fi
  child=$!
  printf '%s\n' "$mode" > "$active_file"
  while [[ "$(cat "$mode_file")" == "$mode" ]]; do
    if ! kill -0 "$child" 2>/dev/null; then
      result=0
      wait "$child" || result=$?
      stop_api
      exit "$result"
    fi
    sleep 1
  done
  stop_api
  mode=$(cat "$mode_file")
done
