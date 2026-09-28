#!/bin/bash
# Build Debug and Release, then diff a fresh snapshot against the committed baseline.
# usage: tools/check.sh            — prints the snapshot diff (empty = no identity change)
#        tools/check.sh --accept   — overwrite snapshots/ with the current state
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/LibraryOfRuina.csproj"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

python3 "$ROOT/tools/check_canonical_getters.py" "$ROOT/src"
dotnet build "$PROJECT" -c Release -nologo -v q -clp:ErrorsOnly
# The verification suites reach into internals; build them too so they do not silently rot.
dotnet build "$ROOT/verification/LibraryOfRuinaVerification.csproj" -c Release -nologo -v q -clp:ErrorsOnly
"$ROOT/tools/snapshot.sh" "$TMP" Debug >/dev/null

if [[ "${1:-}" == "--accept" ]]; then
  cp "$TMP"/*.txt "$ROOT/snapshots/"
  echo "snapshots updated"
else
  diff -ru -x .gdignore "$ROOT/snapshots" "$TMP" && echo "snapshots unchanged"
fi
