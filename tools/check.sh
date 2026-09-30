#!/bin/bash
# Build Debug and Release, then diff a fresh snapshot against the committed baseline.
# usage: tools/check.sh            — prints the snapshot diff (empty = no identity change)
#        tools/check.sh --accept   — overwrite snapshots/ with the current state
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/LibraryOfRuina.csproj"
TMP="$(mktemp -d)"
FIXTURE_OUT="$(mktemp -d)"
trap 'rm -rf "$TMP" "$FIXTURE_OUT"' EXIT

python3 "$ROOT/tools/check_canonical_getters.py" "$ROOT/src"
# The zhs story font is a subset; new zhs text must not use a character outside it.
python3 "$ROOT/tools/check_zhs_font.py"
# No reflection by name outside src/interop/ (syntax-based); the self-test covers the forms that must be caught.
dotnet run --project "$ROOT/tools/PrivateAccessCheck/PrivateAccessCheck.csproj" -c Release -- --self-test "$ROOT/tools/PrivateAccessCheck/fixtures"
dotnet run --project "$ROOT/tools/PrivateAccessCheck/PrivateAccessCheck.csproj" -c Release --no-build -- "$ROOT/src" "$ROOT/tools/private_access_allowlist.txt"
# Players run the library author's release, not our source build: compile once against the published DLL too, so
# the mod cannot depend on library APIs that are not released yet. The normal build below then restores the output.
PUBLISHED_LIB="$(dotnet msbuild "$PROJECT" -nologo -getProperty:SteamRoot)/steamapps/workshop/content/2868840/3747541096/LibraryOfRuinaLib.dll"
if [[ -f "$PUBLISHED_LIB" ]]; then
  dotnet build "$PROJECT" -c Release -nologo -v q -clp:ErrorsOnly -p:LibraryOfRuinaLibDll="$PUBLISHED_LIB"
else
  echo "published LibraryOfRuinaLib not found at $PUBLISHED_LIB; skipping the release-library build" >&2
fi
dotnet build "$PROJECT" -c Release -nologo -v q -clp:ErrorsOnly
# The verification suites reach into internals; build them too so they do not silently rot.
dotnet build "$ROOT/verification/LibraryOfRuinaVerification.csproj" -c Release -nologo -v q -clp:ErrorsOnly
"$ROOT/tools/snapshot.sh" "$TMP" Debug >/dev/null

# The skip-prefix scan must see every patch class form Harmony installs: the fixture program compares the
# shared PatchClassRules with Harmony itself, then the scan of the fixtures must match the expected list.
FIXTURES="$ROOT/tools/PatchRuleFixtures"
dotnet run --project "$FIXTURES/PatchRuleFixtures.csproj" -c Release
dotnet run --project "$ROOT/tools/ModSnapshot/ModSnapshot.csproj" -c Release -- \
  "$FIXTURES/bin/Release/net9.0/PatchRuleFixtures.dll" "$FIXTURE_OUT" "$FIXTURES/bin/Release/net9.0" >/dev/null
diff -u "$FIXTURES/expected_skip_prefixes.txt" "$FIXTURE_OUT/skip_prefixes.txt"
diff -u "$FIXTURES/expected_hook_patches.txt" "$FIXTURE_OUT/hook_patches.txt"

# Every bool prefix must say why it has to skip the original, and every Hook.* patch why it is not a model
# override (design philosophy §1/§3).
for list in skip_prefixes hook_patches; do
  if grep -q $'\tMISSING$' "$TMP/$list.txt"; then
    echo "$list without [LibraryPatch(Reason = ...)]:" >&2
    grep $'\tMISSING$' "$TMP/$list.txt" | cut -f1 >&2
    exit 1
  fi
done

# Types found by order-sensitive discovery (patches, card pools, relics, ally providers) must be listed in
# src/infra/helpers/type_discovery_order.txt; an unlisted one falls back to TypeDef order and moves whenever files move.
# Checked before --accept so accepting cannot hide it.
if grep -qE $'(\tunlisted$|^stale\t)' "$TMP/discovery_order.txt"; then
  echo "type_discovery_order.txt is out of date (add unlisted types in their intended position, drop stale keys):" >&2
  grep -E $'(\tunlisted$|^stale\t)' "$TMP/discovery_order.txt" >&2
  exit 1
fi

if [[ "${1:-}" == "--accept" ]]; then
  cp "$TMP"/*.txt "$ROOT/snapshots/"
  echo "snapshots updated"
else
  diff -ru -x .gdignore -x .DS_Store -x headless "$ROOT/snapshots" "$TMP" && echo "snapshots unchanged"
fi
