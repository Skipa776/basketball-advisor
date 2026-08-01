#!/usr/bin/env bash
# Start the app for local work with your own credentials.
#
# The key never lives in this file. It is read from .env, which .gitignore
# already covers, or from the environment if you would rather export it. That
# split is the whole point: this script is committed, .env is not, and
# safety/secrets_policy.md is explicit that a secret never enters the
# repository.
#
#   1. cp .env.example .env
#   2. put your balldontlie key in .env
#   3. scripts/dev.sh
#
# Flags, all optional:
#   --demo      labelled-fictional sample content on the landing page
#   --open-reg  allow registration even after the instance is claimed
set -euo pipefail
cd "$(dirname "$0")/.."

DEMO=false
OPEN_REG=false
for argument in "$@"; do
    case "$argument" in
        --demo) DEMO=true ;;
        --open-reg) OPEN_REG=true ;;
        -h|--help) sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Unknown option: $argument" >&2; exit 2 ;;
    esac
done

# set -a exports everything the file defines, so the app sees it as
# configuration without this script naming each key. Nothing is echoed.
if [ -f .env ]; then
    set -a
    # shellcheck disable=SC1091
    . ./.env
    set +a
    echo "Loaded .env"
fi

if [ -z "${BallDontLie__ApiKey:-}" ]; then
    cat >&2 <<'MESSAGE'
BallDontLie__ApiKey is not set.

  cp .env.example .env      then put your key in it

.env is gitignored. Do not put the key in this script, in appsettings.json, or
anywhere else that gets committed -- scripts/gate.sh scans every tracked file
for key-shaped strings and will fail the build.

A free key from https://www.balldontlie.io works. Any non-empty value boots the
app; a real one is only needed to import players.
MESSAGE
    exit 1
fi

# Written as if-blocks, not `[ test ] && command`. Under `set -e` the short
# form is a trap: whether a false test ends the script depends on where it sits
# in the and-or list, and the answer is not worth having to remember.
if [ "$DEMO" = true ]; then
    export Demo__Enabled=true
fi

if [ "$OPEN_REG" = true ]; then
    export Auth__OpenRegistration=true
fi

echo "Starting Fastbreak on http://localhost:${PORT:-5280}"
if [ "$DEMO" = true ]; then
    echo "  demo content: on (labelled fictional)"
fi

if [ "$OPEN_REG" = true ]; then
    echo "  open registration: on"
fi

exec scripts/run.sh
