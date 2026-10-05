"""每人一行抽关键帧（待机、蓄力、打击、突刺、斩击、受击、死亡），检查错位、穿地、缺层。
用法：review_sheet.py 输出.png 名字..."""
import sys
from pathlib import Path

from PIL import Image, ImageDraw

dst, names = sys.argv[1], sys.argv[2:]
IDX = [int(v) for v in __import__("os").environ.get("IDX", "20,44,52,88,126,158,222").split(",")]
crop = (100, 100, 1100, 880)
s = 0.3
w, h = round((crop[2] - crop[0]) * s), round((crop[3] - crop[1]) * s)
out = Image.new("RGBA", (w * len(IDX) + 70, h * len(names)), (50, 48, 58, 255))
d = ImageDraw.Draw(out)
for r, n in enumerate(names):
    F = Path(f"/tmp/spine_from_sprite_frames/layers_{n}")
    d.text((4, r * h + 4), n, fill=(255, 220, 120, 255))
    for c, i in enumerate(IDX):
        im = Image.open(F / f"f_{i:03d}.png").convert("RGBA").crop(crop).resize((w, h), Image.LANCZOS)
        x, y = 70 + c * w, r * h
        gy = y + round((830 - crop[1]) * s)
        d.line([(x, gy), (x + w, gy)], fill=(100, 100, 115, 255))
        out.alpha_composite(im, (x, y))
out.convert("RGB").save(dst)
print(out.size)
