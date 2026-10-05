"""按层类型估算染色：原版司书的皮肤、头、头发等层是白底贴图，运行时乘肤色/发色。把分层合成对到模组原图后，
在每类层没被遮挡的像素上取 模组/原版 的中位数比值。比值接近 1 的类型不染色。
用法：type_tints.py <分层目录> <动作> <模组图> → 打印 {类型: [r,g,b]} 与对齐信息（模组图底边中点在原版坐标里的位置）"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
from match_all import match  # noqa: E402

root, motion, mod_path = Path(sys.argv[1]).expanduser(), sys.argv[2], Path(sys.argv[3])
info = json.loads((root / "layers.json").read_text())[motion]
e, dx, dy = match(root / f"{motion}.composite.png", mod_path)
ox, oy = info["composite_origin"]
mod = np.asarray(Image.open(mod_path).convert("RGBA")).astype(np.float32)
H, W = mod.shape[:2]
comp_size = Image.open(root / f"{motion}.composite.png").size
CW, CH = comp_size
top_type = np.full((CH, CW), -1, np.int32)
tex = np.zeros((CH, CW, 3), np.float32)
for l in info["layers"]:
    im = np.asarray(Image.open(root / motion / f"{l['name']}.png").convert("RGBA")).astype(np.float32)
    h, w = im.shape[:2]
    cx, cy = l["center"]
    left, top = round(cx - w / 2 + ox), round(oy - cy - h / 2)
    ys = slice(max(top, 0), min(top + h, CH))
    xs = slice(max(left, 0), min(left + w, CW))
    sub = im[ys.start - top:ys.stop - top, xs.start - left:xs.stop - left]
    opaque = sub[..., 3] > 250
    top_type[ys, xs][opaque] = l["type"]
    tex[ys, xs][opaque] = sub[..., :3][opaque]
tints = {}
for t in sorted(set(np.unique(top_type)) - {-1}):
    yy, xx = np.nonzero(top_type == t)
    my, mx = yy - dy, xx - dx
    ok = (mx >= 0) & (my >= 0) & (mx < W) & (my < H)
    o = tex[yy[ok], xx[ok]]
    m = mod[my[ok], mx[ok]]
    sel = (o.mean(axis=1) > 150) & (m[:, 3] > 250)
    if sel.sum() > 30:
        r = np.median(m[sel][:, :3] / np.maximum(o[sel], 1), axis=0)
        tints[int(t)] = [round(float(v), 3) for v in r]
print(json.dumps({"err": round(e, 3), "bottom_center": [round(dx + W / 2 - ox, 2), round(oy - (dy + H), 2)],
                  "tints": tints}))
