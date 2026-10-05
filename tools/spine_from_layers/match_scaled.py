"""模组贴图是原版合成图按某个倍数缩放过的（如翅振 1.2 倍）时，把每个原版动作的合成图按同一倍数缩放后再精确匹配。
打印每张贴图的最佳动作、误差，和骨架原点（贴图底边中点在原版坐标里的位置，已换回原版单位）。
用法：match_scaled.py <分层目录> <倍数> <贴图>..."""
import json
import sys
import tempfile
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
from match_all import match  # noqa: E402

root, k = Path(sys.argv[1]).expanduser(), float(sys.argv[2])
info = json.loads((root / "layers.json").read_text())
tmp = Path(tempfile.mkdtemp())
for m in info:
    im = Image.open(root / f"{m}.composite.png")
    im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS).save(tmp / f"{m}.png")
for img in sys.argv[3:]:
    p = Path(img)
    cands = sorted((match(tmp / f"{m}.png", p) + (m,)) for m in info)
    e, dx, dy, m = cands[0]
    W, H = Image.open(p).size
    ox, oy = info[m]["composite_origin"]
    print(f"{p.name}: {m} err={e:.2f} | 2nd {cands[1][3]} {cands[1][0]:.2f} | bottom_center="
          f"[{(dx + W / 2) / k - ox:.1f}, {oy - (dy + H) / k:.1f}]", flush=True)
