#!/usr/bin/env bash
# Cut a VaMMCP release: verify, build locally, tag, and publish the DLL to GitHub.
#
# The build has to happen here rather than in CI because it references VaM's own
# Assembly-CSharp.dll, which cannot be redistributed to a public runner.
#
# Usage: ./scripts/release.sh v1.0.0 [--draft]
set -euo pipefail
cd "$(dirname "$0")/.."

TAG="${1:-}"
if [ -z "$TAG" ]; then
  echo "usage: $0 <tag> [--draft]     e.g. $0 v1.0.0" >&2
  exit 2
fi
shift
DRAFT=()
[ "${1:-}" = "--draft" ] && DRAFT=(--draft)

VERSION="${TAG#v}"
command -v gh >/dev/null || { echo "gh (GitHub CLI) is required" >&2; exit 1; }
gh auth status >/dev/null || { echo "run 'gh auth login' first" >&2; exit 1; }

# ---- preflight -------------------------------------------------------------------------
if [ -n "$(git status --porcelain)" ]; then
  echo "working tree is dirty; commit or stash first" >&2
  exit 1
fi

csproj_v=$(grep -oE '<Version>[^<]+</Version>' src/VaMMCP.csproj | sed 's/<[^>]*>//g')
if [ "$csproj_v" != "$VERSION" ]; then
  echo "tag $TAG does not match <Version>$csproj_v</Version> in src/VaMMCP.csproj" >&2
  exit 1
fi

./scripts/check-docs.sh

# ---- build -----------------------------------------------------------------------------
# --build-only: publishing must not depend on writing into a live VaM install
./scripts/deploy.sh --build-only >/dev/null
DLL="src/bin/Release/net35/VaMMCP.dll"
[ -f "$DLL" ] || { echo "build produced no $DLL" >&2; exit 1; }
echo "built $DLL ($(wc -c < "$DLL") bytes)"

# ---- release notes from CHANGELOG.md ---------------------------------------------------
NOTES=$(mktemp)
trap 'rm -f "$NOTES"' EXIT
awk -v v="$VERSION" '
  $0 ~ "^## \\[" v "\\]" { on = 1; next }
  on && /^## \[/ { exit }
  on { print }
' CHANGELOG.md > "$NOTES"
if [ ! -s "$NOTES" ]; then
  echo "CHANGELOG.md has no section for [$VERSION]" >&2
  exit 1
fi
cat >> "$NOTES" <<EOF

---

**Install:** drop \`VaMMCP.dll\` into \`<VaM>\\BepInEx\\plugins\\\` (BepInEx 5.4.x x64 required),
start VaM, then point your MCP client at \`http://127.0.0.1:9837/mcp\`.
See the [README](https://github.com/Rorical/VaMMCP#readme) for the full instructions.
EOF

# ---- tag and publish -------------------------------------------------------------------
if ! git rev-parse "$TAG" >/dev/null 2>&1; then
  git tag -a "$TAG" -m "VaMMCP $VERSION"
fi
git push origin "$TAG"

if gh release view "$TAG" >/dev/null 2>&1; then
  echo "release $TAG already exists; updating notes and asset"
  gh release upload "$TAG" "$DLL#VaMMCP.dll" --clobber
  gh release edit "$TAG" --title "VaMMCP $VERSION" --notes-file "$NOTES" --draft=false
else
  gh release create "$TAG" "$DLL#VaMMCP.dll" \
    --title "VaMMCP $VERSION" \
    --notes-file "$NOTES" \
    "${DRAFT[@]}"
fi

echo "Published $TAG."
