#!/usr/bin/env bash
# Build VaMMCP and deploy the DLL into the VaM BepInEx plugins folder.
# Usage: ./scripts/deploy.sh   (VAM_ROOT defaults to the VaM folder two levels up)
set -euo pipefail
cd "$(dirname "$0")/.."
VAM_ROOT="${VAM_ROOT:-$(cd ../.. && pwd)}"
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
PLUGINS="$VAM_ROOT/BepInEx/plugins"
mkdir -p "$PLUGINS"
cp -v "$DLL" "$PLUGINS/"
echo ""
echo "Deployed. Restart VaM (or reload BepInEx plugins) to load VaMMCP."
