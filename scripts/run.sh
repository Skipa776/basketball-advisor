#!/usr/bin/env bash
# Start the app for local use: check the port, apply migrations, run.
# Configuration comes from the environment because no appsettings.json is
# committed -- see README "Actually running it".
set -euo pipefail
cd "$(dirname "$0")/.."

PORT="${PORT:-5280}"
export PATH="/usr/local/share/dotnet:$PATH"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:$PORT}"
export ConnectionStrings__Fantasy="${ConnectionStrings__Fantasy:-Host=localhost;Port=5432;Database=fantasy_basketball;Username=fantasy;Password=fantasy_local}"

if [ -z "${BallDontLie__ApiKey:-}" ]; then
    echo "BallDontLie__ApiKey is not set. The app requires it at startup." >&2
    echo "  export BallDontLie__ApiKey=your-key" >&2
    echo "Any non-empty value boots; a real key is needed to import players." >&2
    exit 1
fi

# The bind error this avoids names the address but not the process holding it,
# which is a poor place to leave someone whose last run is still alive.
if holder="$(lsof -ti:"$PORT" 2>/dev/null)" && [ -n "$holder" ]; then
    echo "Port $PORT is already in use by PID $holder:" >&2
    ps -p "$holder" -o pid=,command= >&2
    echo "Stop it with 'kill $holder', or run with PORT=5281 scripts/run.sh" >&2
    exit 1
fi

if ! docker compose ps --status running --quiet 2>/dev/null | grep -q .; then
    echo "Starting PostgreSQL..."
    docker compose up -d --wait
fi

dotnet tool install --global dotnet-ef --version 10.0.10 2>/dev/null || true
# --connection is required: the design-time factory hardcodes a separate
# fantasy_design database, and 'database update' prefers it over this config.
dotnet ef database update \
    --project src/FantasyBasketball.Infrastructure \
    --startup-project src/FantasyBasketball.Api \
    --connection "$ConnectionStrings__Fantasy"

echo "Starting on $ASPNETCORE_URLS"
exec dotnet run --project src/FantasyBasketball.Api
