"""把任意一组模组贴图逐张对到某个人物的原版动作（alpha 精确匹配，同 match_all.match），打印最佳与次佳动作、
误差和骨架原点（贴图底边中点在原版坐标里的位置）。用来给 Boss 的每个触发找原版姿势。
用法：match_images.py <分层目录> <贴图>..."""
import json
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
from match_all import match  # noqa: E402

root = Path(sys.argv[1]).expanduser()
info = json.loads((root / "layers.json").read_text())
for img in sys.argv[2:]:
    p = Path(img)
    cands = []
    for m in info:
        e, dx, dy = match(root / f"{m}.composite.png", p)
        cands.append((e, m, dx, dy))
    cands.sort()
    e, m, dx, dy = cands[0]
    W, H = Image.open(p).size
    ox, oy = info[m]["composite_origin"]
    print(f"{p.name}: {m} err={e:.2f} | 2nd {cands[1][1]} {cands[1][0]:.2f} | bottom_center="
          f"[{dx + W / 2 - ox:.1f}, {oy - (dy + H):.1f}]", flush=True)
