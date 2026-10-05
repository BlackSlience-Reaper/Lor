"""把原版分层合成图对到模组现有整图：搜整数偏移使 alpha 最吻合；再在皮肤层不被遮挡的像素上估算肤色（模组图 / 白底贴图）。
用法：match_mod.py <layers 目录> <动作> <模组图>"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

root, motion, mod_path = Path(sys.argv[1]), sys.argv[2], sys.argv[3]
info = json.loads((root / "layers.json").read_text())[motion]
comp = np.asarray(Image.open(root / f"{motion}.composite.png").convert("RGBA")).astype(np.float32)
mod = np.asarray(Image.open(mod_path).convert("RGBA")).astype(np.float32)
H, W = mod.shape[:2]
best = None
for dy in range(0, comp.shape[0] - H + 1):
    for dx in range(0, comp.shape[1] - W + 1):
        a = comp[dy:dy + H, dx:dx + W, 3]
        err = np.abs(a - mod[..., 3]).mean()
        if best is None or err < best[0]:
            best = (err, dx, dy)
err, dx, dy = best
ox, oy = info["composite_origin"]  # 角色根在合成图里的像素位置（y 向下）
# 模组图底边中点在角色根坐标系（像素，y 向上）中的位置
bottom_center = [round(dx + W / 2 - ox, 2), round(oy - (dy + H), 2)]
print(json.dumps({"alpha_err": round(float(err), 3), "offset": [dx, dy], "mod_size": [W, H], "bottom_center_in_root": bottom_center}))

# 肤色：皮肤层（type 3）在最上层且不透明的像素
layers = info["layers"]
cover = np.zeros(comp.shape[:2], dtype=np.int32) - 1
skin_mask = np.zeros(comp.shape[:2], bool)
minx = -ox
maxy = oy
ratios = []
skin_tex = np.zeros(comp.shape[:2] + (3,), np.float32)
for l in layers:
    im = np.asarray(Image.open(root / motion / f"{l['name']}.png").convert("RGBA")).astype(np.float32)
    w, h = l["size"]
    left = round(l["pos"][0] - l["pivot"][0] * w - minx)
    top = round(maxy - (l["pos"][1] + (1 - l["pivot"][1]) * h))
    opaque = im[..., 3] > 250
    region = np.zeros(comp.shape[:2], bool)
    region[top:top + h, left:left + w] = opaque
    skin_mask &= ~region  # 被后画的层盖住
    if l["type"] == 3:
        skin_tex[top:top + h, left:left + w][opaque] = im[..., :3][opaque]
        skin_mask |= region
sel = skin_mask[dy:dy + H, dx:dx + W]
if sel.sum():
    t = skin_tex[dy:dy + H, dx:dx + W][sel]
    m = mod[..., :3][sel]
    bright = t.mean(axis=1) > 200
    ratio = (m[bright] / np.maximum(t[bright], 1)).mean(axis=0)
    print("skin pixels", int(sel.sum()), "tint", [round(float(v), 3) for v in ratio])
