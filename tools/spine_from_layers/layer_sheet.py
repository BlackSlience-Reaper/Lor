"""每个人物一行：待机动作的各层（名字、类型、排序）并排，最左是合成图，用来人工决定哪些层挂手臂/武器骨头。
用法：layer_sheet.py <分层根目录> <输出.png> <动作后缀，默认 Default> 名字...
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

root, dst, suffix, names = Path(sys.argv[1]).expanduser(), sys.argv[2], sys.argv[3], sys.argv[4:]
TYPES = {0: "前发", 1: "后发", 2: "身体", 3: "皮肤", 4: "脸", 5: "头", 6: "兜帽", 7: "面具", 9: "特效", 10: "敌方"}
font = ImageFont.truetype("/System/Library/Fonts/Hiragino Sans GB.ttc", 13)
S = 0.3
rows = []
for n in names:
    info = json.loads((root / n / "layers.json").read_text())
    motion = next(m for m in info if m.endswith("_" + suffix))
    comp = Image.open(root / n / f"{motion}.composite.png").convert("RGBA")
    tiles = [(f"{n}", comp)]
    for l in info[motion]["layers"]:
        tiles.append((f"{l['name']}·{TYPES.get(l['type'], l['type'])}·{l['order']}",
                      Image.open(root / n / motion / f"{l['name']}.png").convert("RGBA")))
    tiles = [(t, im.resize((max(1, round(im.width * S)), max(1, round(im.height * S))))) for t, im in tiles]
    W = sum(max(im.width, 90) + 8 for _, im in tiles)
    H = max(im.height for _, im in tiles) + 18
    r = Image.new("RGBA", (W, H), (70, 70, 85, 255))
    d = ImageDraw.Draw(r)
    x = 0
    for t, im in tiles:
        r.alpha_composite(im, (x, 16))
        d.text((x, 0), t, font=font, fill=(255, 220, 120))
        x += max(im.width, 90) + 8
    rows.append(r)
W = max(r.width for r in rows)
out = Image.new("RGBA", (W, sum(r.height + 4 for r in rows)), (40, 40, 50, 255))
y = 0
for r in rows:
    out.alpha_composite(r, (0, y))
    y += r.height + 4
out.convert("RGB").save(dst)
print(out.size)
