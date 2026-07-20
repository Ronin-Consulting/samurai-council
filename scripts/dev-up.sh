#!/usr/bin/env bash
#
# dev-up.sh — launch the full SamurAI Council stack in Docker.
#
# One image (SamurAICouncil.Api/Dockerfile) builds the Angular SPA and the .NET
# API, then serves the SPA from the API's wwwroot — so "Angular + backend" run
# together at http://localhost:5080. Postgres (app data) and SQL Server
# (ContosoRetailDW / Text-to-SQL) come up alongside it.
#
# The image is rebuilt from the CURRENT working tree on every run, so whatever
# you have on disk — committed or not — is what you see in the browser.
#
# Usage:
#   ./scripts/dev-up.sh                     build + (re)start everything, wait until it answers
#   ./scripts/dev-up.sh logs                follow the web container logs
#   ./scripts/dev-up.sh down                stop and remove the containers
#   ./scripts/dev-up.sh rebuild             force a clean rebuild (no image cache)
#
#   ./scripts/dev-up.sh up openrouter       same, but using the .env.openrouter profile
#                                           (open-source models via OpenRouter — see
#                                           .env.openrouter.example). Any subcommand accepts
#                                           a profile as the 2nd arg: logs/down/rebuild openrouter.
#
# On this host Docker runs inside WSL2. Run this from a WSL shell, or use the
# PowerShell wrapper (scripts/dev-up.ps1) from Windows.
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

URL="http://localhost:5080"
CMD="${1:-up}"
PROFILE="${2:-}"

ENV_FILE=".env"
COMPOSE_ARGS=(docker compose)
if [ -n "$PROFILE" ]; then
  ENV_FILE=".env.$PROFILE"
  COMPOSE_ARGS=(docker compose --env-file "$ENV_FILE")
fi

case "$CMD" in
  down)
    echo "▶ Stopping SamurAI Council… [profile: ${PROFILE:-default}]"
    "${COMPOSE_ARGS[@]}" down
    exit 0
    ;;
  logs)
    "${COMPOSE_ARGS[@]}" logs -f web
    exit 0
    ;;
  up|rebuild) ;;
  *)
    echo "Usage: $0 [up|logs|down|rebuild] [profile]" >&2
    echo "  e.g.  $0 up openrouter" >&2
    exit 2
    ;;
esac

# ---- preflight ----
if ! docker info >/dev/null 2>&1; then
  echo "ERROR: Docker daemon not reachable." >&2
  echo "       In WSL, start it with:  sudo service docker start" >&2
  exit 1
fi

if [ ! -f "$ENV_FILE" ]; then
  echo "ERROR: $ENV_FILE not found in $ROOT." >&2
  if [ -n "$PROFILE" ]; then
    echo "       Create it:  cp .env.$PROFILE.example .env.$PROFILE  and set OPENAI_API_KEY." >&2
  else
    echo "       Create it:  cp .env.example .env  and set OPENAI_API_KEY (the FortyAU gateway token)." >&2
  fi
  exit 1
fi

# OPENAI_API_KEY holds the gateway/provider token and is required by compose.
key="$(grep -E '^OPENAI_API_KEY=' "$ENV_FILE" | head -1 | cut -d= -f2- || true)"
if [ -z "${key//[[:space:]]/}" ] || printf '%s' "$key" | grep -qi 'your-'; then
  echo "ERROR: OPENAI_API_KEY is empty or still the placeholder in $ENV_FILE." >&2
  exit 1
fi

# ---- build + start ----
echo "▶ Building images from the current source and starting containers… [profile: ${PROFILE:-default}]"
echo "  (The Angular SPA is built inside the image, so uncommitted UI + backend changes are included.)"
if [ "$CMD" = "rebuild" ]; then
  "${COMPOSE_ARGS[@]}" build --no-cache
  "${COMPOSE_ARGS[@]}" up -d
else
  "${COMPOSE_ARGS[@]}" up -d --build
fi

# ---- wait until the app answers ----
printf "▶ Waiting for %s " "$URL"
ok=0
for ((i = 1; i <= 90; i++)); do
  if curl -fsS -o /dev/null "$URL"; then ok=1; break; fi
  printf "."
  sleep 2
done
echo

if [ "$ok" -ne 1 ]; then
  echo "WARN: the app did not respond within ~3 minutes. Recent web logs:" >&2
  "${COMPOSE_ARGS[@]}" logs --tail 50 web >&2
  exit 1
fi

PROFILE_SUFFIX=""
[ -n "$PROFILE" ] && PROFILE_SUFFIX=" $PROFILE"

cat <<EOF

✅ SamurAI Council is running. [profile: ${PROFILE:-default}]

   App:          $URL
   Follow logs:  ./scripts/dev-up.sh logs${PROFILE_SUFFIX}      (or: docker compose $([ -n "$PROFILE" ] && echo "--env-file $ENV_FILE ")logs -f web)
   Stop:         ./scripts/dev-up.sh down${PROFILE_SUFFIX}

   Re-run this script any time to rebuild with your latest changes.
EOF
