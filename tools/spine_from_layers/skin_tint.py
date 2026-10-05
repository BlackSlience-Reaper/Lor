"""按 match_all.py 已对好的位置，在各姿势里取皮肤层（白底，运行时乘肤色）没被遮挡的像素，
与模组原图比出肤色，写回 match.json 的 skin_tint。用法：skin_tint.py <match.json> <分层根目录> <模组 images/monsters>"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

mpath, root, mod_root = Path(sys.argv[1]), Path(sys.argv[2]).expanduser(), Path(sys.argv[3])
match = json.loads(mpath.read_text())
for name, m in match.items():
    info = json.loads((root / m["layers"] / "layers.json").read_text())
    samples_t, samples_m = [], []
    for kind, p in m["poses"].items():
        motion = p["motion"]
        ox, oy = info[motion]["composite_origin"]
        W, H = p["size"]
        dx = round(p["bottom_center"][0] - W / 2 + ox)
        dy = round(oy - p["bottom_center"][1] - H)
        comp = Image.open(root / m["layers"] / f"{motion}.composite.png")
        CW, CH = comp.size
        mask = np.zeros((CH, CW), bool)
        tex = np.zeros((CH, CW, 3), np.float32)
        for l in info[motion]["layers"]:
            im = np.asarray(Image.open(root / m["layers"] / motion / f"{l['name']}.png").convert("RGBA")).astype(np.float32)
            h, w = im.shape[:2]
            cx, cy = l["center"]
            left, top = round(cx - w / 2 + ox), round(oy - cy - h / 2)
            sub = (slice(max(top, 0), min(top + h, CH)), slice(max(left, 0), min(left + w, CW)))
            im = im[sub[0].start - top:sub[0].stop - top, sub[1].start - left:sub[1].stop - left]
            opaque = im[..., 3] > 250
            mask[sub][opaque] = l["type"] == 3
            if l["type"] == 3:
                tex[sub][opaque] = im[..., :3][opaque]
        mod = np.asarray(Image.open(mod_root / Path(m["prefix"]).parent / p["mod"]).convert("RGBA")).astype(np.float32)
        # 模组图放到合成图坐标
        ys, xs = np.nonzero(mask)
        mx, my = xs - dx, ys - dy
        ok = (mx >= 0) & (my >= 0) & (mx < W) & (my < H)
        t = tex[ys[ok], xs[ok]]
        mm = mod[my[ok], mx[ok]]
        bright = (t.mean(axis=1) > 200) & (mm[:, 3] > 250)
        samples_t.append(t[bright])
        samples_m.append(mm[bright][:, :3])
    t = np.concatenate(samples_t) if samples_t else np.zeros((0, 3))
    mm = np.concatenate(samples_m) if samples_m else np.zeros((0, 3))
    if len(t) > 5:
        tint = np.median(mm / np.maximum(t, 1), axis=0)
        m["skin_tint"] = [round(float(v), 3) for v in tint]
    print(name, len(t), m.get("skin_tint"))
mpath.write_text(json.dumps(match, ensure_ascii=False, indent=1))
