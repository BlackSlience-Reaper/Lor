#!/bin/bash
# 构建当前 LibraryOfRuinaLib 源码；不 fetch、checkout 或覆盖第三方工程文件。
# 使用前按 docs/双版本适配.md 准备双版本分支。
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="${LibraryOfRuinaLibSource:-$ROOT/build/LibraryOfRuinaLib}"
TARGET="${1:-${CompatibilityTarget:-0.111.0}}"
case "$TARGET" in 0.107.1|0.111.0) ;; *) echo "不支持的目标：$TARGET" >&2; exit 2;; esac
if [[ ! -f "$SRC/Directory.Build.props" ]] || ! grep -q 'CompatibilityTarget' "$SRC/Directory.Build.props"; then
  echo "请先把 LibraryOfRuinaLib 双版本源码放到 $SRC；不会自动改动已有检出。" >&2
  exit 2
fi
dotnet build "$SRC/LibraryLib.csproj" -c Release -nologo -p:CompatibilityTarget="$TARGET"
