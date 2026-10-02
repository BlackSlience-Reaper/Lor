#!/bin/bash
# Build the mod and write metadata snapshots (models, SavedProperty set, Harmony patches, statics).
# usage: tools/snapshot.sh [out-dir] [Debug|Release]
#   default out-dir: snapshots/  — commit it; a refactor step must leave it unchanged unless the
#   change is intended, in which case the diff is the review evidence.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export CompatibilityTarget="${CompatibilityTarget:-0.111.0}"
OUT="${1:-$ROOT/snapshots/$CompatibilityTarget}"
CONFIG="${2:-Debug}"
PROJECT="$ROOT/LibraryOfRuina.csproj"

prop() {
  dotnet msbuild "$PROJECT" -nologo -getProperty:"$1" -p:Configuration="$CONFIG"
}

dotnet build "$PROJECT" -c "$CONFIG" -nologo -v q -clp:ErrorsOnly
DLL="$(prop TargetPath)"

DATA_DIR="$(prop Sts2DataDir)"
RITSU_ROOT="$(prop RitsuLibRoot)"
RITSU_TARGET="$(prop RitsuLibReferenceTarget)"
ACT_DLL="$(prop ActLikeIt2Dll)"
LIB_DLL="$(prop LibraryOfRuinaLibDll)"
GODOT_SHARP="$(find "${NUGET_PACKAGES:-$HOME/.nuget/packages}/godotsharp" -name GodotSharp.dll -path '*4.5.1*' | head -n 1)"

dotnet run --project "$ROOT/tools/ModSnapshot/ModSnapshot.csproj" -c Release -- \
  "$DLL" "$OUT" \
  "$RITSU_ROOT/compat/$RITSU_TARGET" "$RITSU_ROOT/shared" \
  "$ACT_DLL" "$LIB_DLL" "$GODOT_SHARP" "$DATA_DIR"
