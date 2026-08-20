#!/usr/bin/env bash
# Consistency checks that do not need VaM: the tool registry, the docs and the version
# numbers must agree. Run by CI and by scripts/release.sh.
set -euo pipefail
cd "$(dirname "$0")/.."

REGISTRY="src/Mcp/ToolRegistry.cs"
fail=0
note() { echo "  $*"; }
err() { echo "FAIL: $*"; fail=1; }

# ---- tool names as actually registered -------------------------------------------------
tools=$(grep -oE 'new Tool\("[a-z_]+"' "$REGISTRY" | sed 's/.*"\(.*\)"/\1/' | sort)
count=$(echo "$tools" | wc -l | tr -d ' ')
echo "== $count tools registered in $REGISTRY"

dupes=$(echo "$tools" | uniq -d)
[ -n "$dupes" ] && err "duplicate tool names: $(echo "$dupes" | tr '\n' ' ')"

# ---- every tool is documented, in both languages ---------------------------------------
for doc in docs/TOOLS.md docs/TOOLS.zh-CN.md; do
  missing=""
  for t in $tools; do
    grep -q "\`$t\`\|^### $t" "$doc" || missing="$missing $t"
  done
  if [ -n "$missing" ]; then
    err "$doc does not document:$missing"
  else
    note "$doc documents all $count tools"
  fi
done

# ---- no docs table row invents a tool that does not exist ------------------------------
for doc in docs/TOOLS.md docs/TOOLS.zh-CN.md; do
  bogus=""
  # shellcheck disable=SC2016  # the backticks below are markdown, not command substitution
  while IFS= read -r name; do
    echo "$tools" | grep -qx "$name" || bogus="$bogus $name"
  done < <(grep -E '^\| *`' "$doc" | cut -d'|' -f2 | grep -oE '`[a-z_]+`' | tr -d '`' | sort -u)
  [ -n "$bogus" ] && err "$doc lists unknown tools:$bogus"
done

# ---- the advertised count matches ------------------------------------------------------
for doc in README.md README.zh-CN.md docs/TOOLS.md docs/TOOLS.zh-CN.md; do
  grep -q "$count" "$doc" || err "$doc never mentions the tool count ($count)"
done
note "tool count $count referenced by README and TOOLS docs"

# ---- version numbers agree -------------------------------------------------------------
csproj_v=$(grep -oE '<Version>[^<]+</Version>' src/VaMMCP.csproj | sed 's/<[^>]*>//g')
plugin_v=$(grep -oE 'BepInPlugin\("[^"]+", *"[^"]+", *"[^"]+"\)' src/Plugin.cs | sed 's/.*, *"\([^"]*\)")/\1/')
server_v=$(grep -oE 'ServerVersion *= *"[^"]+"' src/Mcp/McpServer.cs | sed 's/.*"\(.*\)"/\1/')
if [ "$csproj_v" = "$plugin_v" ] && [ "$csproj_v" = "$server_v" ]; then
  note "version $csproj_v consistent across csproj / Plugin.cs / McpServer.cs"
else
  err "version mismatch: csproj=$csproj_v Plugin.cs=$plugin_v McpServer.cs=$server_v"
fi
grep -q "\[$csproj_v\]" CHANGELOG.md || err "CHANGELOG.md has no entry for $csproj_v"

# ---- shell scripts are executable in git -----------------------------------------------
if git rev-parse --git-dir >/dev/null 2>&1; then
  nonexec=""
  while IFS= read -r line; do
    mode=$(echo "$line" | cut -d' ' -f1)
    path=$(echo "$line" | cut -f2)
    if [ "$mode" != "100755" ]; then
      nonexec="$nonexec $path"
      err "$path is not executable in git (git update-index --chmod=+x $path)"
    fi
  done < <(git ls-files -s -- 'scripts/*.sh')
  [ -n "$nonexec" ] || note "scripts/*.sh executable bits ok"
fi

# ---- no references to local-only VaM artefacts -----------------------------------------
if git rev-parse --git-dir >/dev/null 2>&1; then
  # exclude this file, which necessarily contains the pattern it looks for
  if git grep -nI 'src2/' -- . ':(exclude)scripts/check-docs.sh' >/dev/null 2>&1; then
    err "tracked files reference src2/ (a local decompile, not something VaM ships):"
    git grep -nI 'src2/' -- . ':(exclude)scripts/check-docs.sh' | sed 's/^/    /'
  else
    note "no references to local decompiled sources"
  fi
fi

# ---- nothing machine-specific leaked into tracked files --------------------------------
if git rev-parse --git-dir >/dev/null 2>&1; then
  if git grep -nIE '(/home/[a-z0-9_-]+/|[A-Z]:\\Users\\|/mnt/[a-z]/Data/)' -- . >/dev/null 2>&1; then
    err "tracked files contain machine-specific paths:"
    git grep -nIE '(/home/[a-z0-9_-]+/|[A-Z]:\\Users\\|/mnt/[a-z]/Data/)' -- . | sed 's/^/    /'
  else
    note "no machine-specific paths in tracked files"
  fi
fi

echo
if [ "$fail" -eq 0 ]; then
  echo "All checks passed."
else
  echo "Some checks failed."
  exit 1
fi
