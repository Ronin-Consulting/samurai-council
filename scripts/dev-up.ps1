# dev-up.ps1 — Windows wrapper for scripts/dev-up.sh.
#
# Docker runs inside WSL2 on this host, so this delegates to the bash script
# there. Run it from Windows PowerShell at the repo root:
#
#   .\scripts\dev-up.ps1                      build + (re)start, wait until it answers
#   .\scripts\dev-up.ps1 logs                 follow the web logs
#   .\scripts\dev-up.ps1 down                 stop the stack
#   .\scripts\dev-up.ps1 rebuild              clean rebuild (no cache)
#
#   .\scripts\dev-up.ps1 up openrouter        same, but using the .env.openrouter profile
#                                             (open-source models via OpenRouter)
#
param(
  [ValidateSet("up", "logs", "down", "rebuild")]
  [string]$Command = "up",

  # Optional env-file profile (e.g. "openrouter" -> .env.openrouter). Empty = default .env.
  # (Named $EnvProfile, not $Profile, to avoid shadowing PowerShell's automatic $PROFILE variable.)
  [string]$EnvProfile = ""
)

$ErrorActionPreference = "Stop"
$Distro = "Ubuntu"   # WSL distro that hosts Docker on this machine

# Repo root = parent of this scripts directory; translate to a WSL path.
$root = Split-Path -Parent $PSScriptRoot
$wslRoot = (& wsl.exe -d $Distro wslpath -a "$root").Trim()

if ($EnvProfile) {
  & wsl.exe -d $Distro -- bash "$wslRoot/scripts/dev-up.sh" $Command $EnvProfile
} else {
  & wsl.exe -d $Distro -- bash "$wslRoot/scripts/dev-up.sh" $Command
}
exit $LASTEXITCODE
