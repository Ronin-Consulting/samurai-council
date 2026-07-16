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
#   ./scripts/dev-up.sh            build + (re)start everything, wait until it answers
#   ./scripts/dev-up.sh logs       follow the web container logs
#   ./scripts/dev-up.sh down       stop and remove the containers
#   ./scripts/dev-up.sh rebuild    force a clean rebuild (no image cache)
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

case "$CMD" in
  down)
    echo "▶ Stopping SamurAI Council…"
    docker compose down
    exit 0
    ;;
  logs)
    docker compose logs -f web
    exit 0
    ;;
  up|rebuild) ;;
  *)
    echo "Usage: $0 [up|logs|down|rebuild]" >&2
    exit 2
    ;;
esac

# ---- preflight ----
if ! docker info >/dev/null 2>&1; then
  echo "ERROR: Docker daemon not reachable." >&2
  echo "       In WSL, start it with:  sudo service docker start" >&2
  exit 1
fi

if [ ! -f .env ]; then
  echo "ERROR: .env not found in $ROOT." >&2
  echo "       Create it:  cp .env.example .env  and set OPENAI_API_KEY (the FortyAU gateway token)." >&2
  exit 1
fi

# OPENAI_API_KEY holds the gateway token and is required by compose.
key="$(grep -E '^OPENAI_API_KEY=' .env | head -1 | cut -d= -f2- || true)"
if [ -z "${key//[[:space:]]/}" ] || printf '%s' "$key" | grep -q 'your-openai-api-key-here'; then
  echo "ERROR: OPENAI_API_KEY is empty or still the placeholder in .env." >&2
  echo "       Set it to the FortyAU gateway token." >&2
  exit 1
fi

# ---- build + start ----
echo "▶ Building images from the current source and starting containers…"
echo "  (The Angular SPA is built inside the image, so uncommitted UI + backend changes are included.)"
if [ "$CMD" = "rebuild" ]; then
  docker compose build --no-cache
  docker compose up -d
else
  docker compose up -d --build
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
  docker compose logs --tail 50 web >&2
  exit 1
fi

cat <<EOF

✅ SamurAI Council is running.

   App:          $URL
   Follow logs:  ./scripts/dev-up.sh logs      (or: docker compose logs -f web)
   Stop:         ./scripts/dev-up.sh down

   Re-run this script any time to rebuild with your latest changes.
EOF
