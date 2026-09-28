#!/bin/bash
# Build LibraryOfRuinaLib from its GitHub source so the mod compiles against the library's latest API.
# The checkout lives in build/LibraryOfRuinaLib (git-ignored); Directory.Build.props picks up the DLL from
# build/LibraryOfRuinaLib/bin/out/ when local.props does not set LibraryOfRuinaLibDll.
# usage: tools/build_lib.sh [git-ref]      default ref: origin/main
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/build/LibraryOfRuinaLib"
REPO="https://github.com/Xuyuha/LibraryOfRuinaLib.git"
REF="${1:-origin/main}"

if [[ ! -d "$SRC/.git" ]]; then
  git clone -q "$REPO" "$SRC"
fi
git -C "$SRC" fetch -q origin
git -C "$SRC" checkout -q --detach "$REF"
cp "$ROOT/tools/LibraryOfRuinaLib.csproj.template" "$SRC/LibraryLib.csproj"

DATA_DIR="$(dotnet msbuild "$ROOT/LibraryOfRuina.csproj" -nologo -getProperty:Sts2DataDir)"
dotnet build "$SRC/LibraryLib.csproj" -c Release -nologo -v q -clp:ErrorsOnly \
  -p:Sts2DataDir="$DATA_DIR" -o "$SRC/bin/out" >&2

echo "LibraryOfRuinaLib $(git -C "$SRC" describe --always --tags) ($(git -C "$SRC" log -1 --format=%s)) -> $SRC/bin/out/LibraryOfRuinaLib.dll"
