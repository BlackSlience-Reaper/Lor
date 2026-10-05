#!/bin/bash
# 从原版人物包导出来宾分层到 ~/.local/share/LibraryOfRuina-layers/<名字>（原版游戏文件在 ~/.local/share/LibraryOfRuina-win）。
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="${LOR_LAYERS:-$HOME/.local/share/LibraryOfRuina-layers}"
mkdir -p "$OUT"
while read -r name bundle; do
  [ -z "$name" ] && continue
  rm -rf "$OUT/$name"
  python3 "$HERE/export_layers.py" "$bundle" "$OUT/$name" > /dev/null
  echo "$name <- $bundle"
done <<'LIST'
eri char_460
yun char_627
philip char_553
salvador char_576
yuna char_628
oscar char_546
pamela char_550
pameli char_551
gin char_467
yang char_619
sayo_f char_578
sayo_n char_579
mccullin char_518
naoki char_532
taein char_590
arnold char_413
consta char_445
mo char_527
finn char_462
meow_f char_520
meow_n char_521
mu_mu char_528
oink char_541
LIST
