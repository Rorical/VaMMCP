#!/usr/bin/env bash
# Install BepInEx 5.4.21 x64 into the VaM folder (additive: no existing file is overwritten).
set -euo pipefail
cd "$(dirname "$0")/.."
VAM_ROOT="${VAM_ROOT:-$(cd .. && pwd)}"
if [ ! -d "$VAM_ROOT/VaM_Data" ]; then
  echo "No VaM_Data in '$VAM_ROOT' — that does not look like a VaM install." >&2
  echo "Set VAM_ROOT, e.g. VAM_ROOT=\"D:/path/to/VaM\" $0" >&2
  exit 1
fi
VER="5.4.21"
if [ -f "$VAM_ROOT/winhttp.dll" ]; then
  echo "BepInEx already installed ($VAM_ROOT/winhttp.dll present)."
  exit 0
fi
ZIP="$VAM_ROOT/.bepinex.zip"
trap 'rm -f "$ZIP"' EXIT
echo "Downloading BepInEx $VER ..."
curl -sL -o "$ZIP" "https://github.com/BepInEx/BepInEx/releases/download/v$VER/BepInEx_x64_$VER.0.zip"
unzip -o -q "$ZIP" -d "$VAM_ROOT"
echo "BepInEx $VER installed into $VAM_ROOT (winhttp.dll + BepInEx/)."
echo "Run VaM once to initialize BepInEx (it will create BepInEx/plugins, BepInEx/config)."
