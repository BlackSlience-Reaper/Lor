"""把某个动作的分层合成放大并画上角色根坐标网格（像素，y 向上），用来读转轴点。
用法：grid_motion.py <layers 目录> <动作> <输出.png> [只画这些层,逗号分隔]"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

root, motion, dst = Path(sys.argv[1]), sys.argv[2], sys.argv[3]
only = set(sys.argv[4].split(",")) if len(sys.argv) > 4 else None
info = json.loads((root / "layers.json").read_text())[motion]
ox, oy = info["composite_origin"]
comp = Image.new("RGBA", Image.open(root / f"{motion}.composite.png").size)
for l in info["layers"]:
    if only and l["name"] not in only:
        continue
    w, h = l["size"]
    left = round(l["pos"][0] - l["pivot"][0] * w + ox)
    top = round(oy - (l["pos"][1] + (1 - l["pivot"][1]) * h))
    comp.alpha_composite(Image.open(root / motion / f"{l['name']}.png").convert("RGBA"), (left, top))
S = 1.5
pad = 40
img = Image.new("RGBA", (round(comp.width * S) + 2 * pad, round(comp.height * S) + 2 * pad), (235, 235, 240, 255))
img.alpha_composite(comp.resize((round(comp.width * S), round(comp.height * S))), (pad, pad))
d = ImageDraw.Draw(img)
font = ImageFont.truetype("/System/Library/Fonts/Hiragino Sans GB.ttc", 13)
step = 20
x0 = -int(ox // step) * step
for x in range(x0, int(comp.width - ox) + step, step):
    px = pad + (x + ox) * S
    major = x % 100 == 0
    d.line([(px, pad), (px, img.height - pad)], fill=(255, 0, 0, 140) if major else (120, 120, 255, 60))
    if major:
        d.text((px + 2, 2), str(x), font=font, fill=(200, 0, 0))
for y in range(-int((comp.height - oy) // step) * step - step, int(oy) + step, step):
    py = pad + (oy - y) * S
    major = y % 100 == 0
    d.line([(pad, py), (img.width - pad, py)], fill=(255, 0, 0, 140) if major else (120, 120, 255, 60))
    if major:
        d.text((2, py + 2), str(y), font=font, fill=(200, 0, 0))
img.convert("RGB").save(dst)
print(img.size)
