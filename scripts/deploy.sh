#!/usr/bin/env bash
# Build VaMMCP and deploy the DLL into the VaM BepInEx plugins folder.
#
# Usage: ./scripts/deploy.sh [--build-only] [extra dotnet build args...]
#   VAM_ROOT defaults to the folder containing this repo.
#   --build-only skips the copy, which is what you want while VaM is running: Windows locks the
#   loaded DLL, so deploying to a live game fails.
set -euo pipefail
cd "$(dirname "$0")/.."

BUILD_ONLY=0
if [ "${1:-}" = "--build-only" ]; then
  BUILD_ONLY=1
  shift
fi
VAM_ROOT="${VAM_ROOT:-$(cd .. && pwd)}"
if [ ! -d "$VAM_ROOT/VaM_Data" ]; then
  echo "No VaM_Data in '$VAM_ROOT' — that does not look like a VaM install." >&2
  echo "Set VAM_ROOT, e.g. VAM_ROOT=\"D:/path/to/VaM\" $0" >&2
  exit 1
fi
echo "VaM root: $VAM_ROOT"

# dotnet first-run needs a writable HOME (the sandbox/CI HOME may be read-only)
export HOME="${HOME:-$(pwd)/.home}"
export DOTNET_CLI_HOME="$HOME"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NUGET_PACKAGES="${NUGET_PACKAGES:-$(pwd)/.nuget/packages}"
export NUGET_HTTP_CACHE_PATH="${NUGET_HTTP_CACHE_PATH:-$(pwd)/.nuget/http-cache}"
mkdir -p "$HOME"

dotnet build src/VaMMCP.csproj -c Release "$@"

DLL="src/bin/Release/net35/VaMMCP.dll"
if [ "$BUILD_ONLY" = "1" ]; then
  echo "Built $DLL (not deployed)."
  exit 0
fi

PLUGINS="$VAM_ROOT/BepInEx/plugins"
mkdir -p "$PLUGINS"
if ! cp "$DLL" "$PLUGINS/VaMMCP.dll"; then
  echo "Could not write $PLUGINS/VaMMCP.dll — is VaM running? Windows locks the loaded DLL." >&2
  exit 1
fi
echo "Deployed $DLL -> $PLUGINS/VaMMCP.dll"
echo "Restart VaM to load it (BepInEx loads plugins at startup)."
