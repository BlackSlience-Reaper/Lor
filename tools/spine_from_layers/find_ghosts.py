"""找会产生重影的层：同一姿势里两层图像大面积重合、重合处颜色也几乎一样（原版把同一件饰物画了两份，静止时完全盖住），
却挂在不同的骨头上——动画里两根骨头各动各的，两份就错开了（例如闪金冲锋起手姿势的两顶皇冠）。
用法：find_ghosts.py <配置.json>...   打印 配置 姿势 层A(骨) 层B(骨) 重合占比 色差"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image


def placed(root, motion, l):
    im = Image.open(root / motion / f"{l['name']}.png").convert("RGBA")
    k = 100.0 / (l.get("ppu") or 100.0)
    sx, sy = l["scale"][0] * k, l["scale"][1] * k
    if abs(abs(sx) - 1) > 1e-3 or abs(abs(sy) - 1) > 1e-3:
        im = im.resize((max(1, round(im.width * abs(sx))), max(1, round(im.height * abs(sy)))))
    if abs(l.get("rot", 0)) > 1e-3:
        im = im.rotate(l["rot"], expand=True)
    a = np.asarray(im).astype(np.float32)
    cx, cy = l["center"]
    return a, round(cx - im.width / 2), round(cy + im.height / 2)  # 左上角（y 向上）


for cfg_path in sys.argv[1:]:
    cfg = json.loads(Path(cfg_path).read_text())
    root = Path(cfg["layers_dir"]).expanduser()
    info = json.loads((root / "layers.json").read_text())
    for pose, m in cfg["motions"].items():
        layers = [l for l in info[m["source"]]["layers"] if l["name"] in m["assign"]]
        ims = {l["name"]: placed(root, m["source"], l) for l in layers}
        for i, la in enumerate(layers):
            for lb in layers[i + 1:]:
                ba, bb = m["assign"][la["name"]], m["assign"][lb["name"]]
                if ba == bb:
                    continue
                a, ax, ay = ims[la["name"]]
                b, bx, by = ims[lb["name"]]
                x0, x1 = max(ax, bx), min(ax + a.shape[1], bx + b.shape[1])
                y1, y0 = min(ay, by), max(ay - a.shape[0], by - b.shape[0])
                if x1 <= x0 or y1 <= y0:
                    continue
                pa = a[ay - y1:ay - y0, x0 - ax:x1 - ax]
                pb = b[by - y1:by - y0, x0 - bx:x1 - bx]
                both = (pa[..., 3] > 128) & (pb[..., 3] > 128)
                small = min((a[..., 3] > 128).sum(), (b[..., 3] > 128).sum())
                if small < 400 or both.sum() < 0.5 * small:
                    continue
                diff = float(np.abs(pa[both][:, :3] - pb[both][:, :3]).mean())
                if diff < 25:
                    print(f"{Path(cfg_path).name} {pose} {la['name']}({ba}) {lb['name']}({bb}) "
                          f"overlap={both.sum() / small:.2f} diff={diff:.1f}", flush=True)
