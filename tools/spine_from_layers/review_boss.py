"""每个 Boss 一行：待机、各段动画的蓄力帧与姿势帧、死亡末帧。用法：review_boss.py 输出.png 名字..."""
import sys
from pathlib import Path

from PIL import Image, ImageDraw

dst, names = sys.argv[1], sys.argv[2:]
crop = (100, 200, 1500, 1000)
s = 0.2
w, h = round((crop[2] - crop[0]) * s), round((crop[3] - crop[1]) * s)
rows = []
for n in names:
    F = Path(f"/tmp/spine_from_sprite_frames/boss_{n}")
    labels = (F / "labels.txt").read_text().splitlines()
    idx, prev = [10], None
    for i, lab in enumerate(labels):
        if lab != prev and lab != "idle":
            idx += [i + 3, i + 9] if lab != "die" else []
        prev = lab
    idx.append(len(labels) - 12)
    row = Image.new("RGBA", (w * len(idx), h), (50, 48, 58, 255))
    d = ImageDraw.Draw(row)
    for c, i in enumerate(idx):
        im = Image.open(F / f"f_{i:03d}.png").convert("RGBA").crop(crop).resize((w, h), Image.LANCZOS)
        row.alpha_composite(im, (c * w, 0))
        d.text((c * w + 2, 2), f"{labels[i]}", fill=(255, 220, 120, 255))
    rows.append(row)
out = Image.new("RGBA", (max(r.width for r in rows), sum(r.height for r in rows)), (40, 40, 50, 255))
y = 0
for r in rows:
    out.alpha_composite(r, (0, y))
    y += r.height
out.convert("RGB").save(dst)
print(out.size)
