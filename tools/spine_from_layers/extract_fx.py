"""模组原图 = 原版某个动作的分层合成 + 模组作者叠上去的特效（原版里单独播放的弹道、光束等）时，把特效抠出来。
先在原图里找合成图的位置（只比合成图不透明的部分，避开特效），再把与合成图不同的像素（合成图外的、或颜色差很多的）
存成一张特效图，打印它在原版角色坐标里的中心，供 spine_from_layers 配置的 extra 层使用。
用法：extract_fx.py <分层目录> <动作> <模组图> <输出.png>"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

root, motion, mod_path, out = Path(sys.argv[1]).expanduser(), sys.argv[2], Path(sys.argv[3]), Path(sys.argv[4])
info = json.loads((root / "layers.json").read_text())[motion]
comp = np.asarray(Image.open(root / f"{motion}.composite.png").convert("RGBA")).astype(np.float32)
mod = np.asarray(Image.open(mod_path).convert("RGBA")).astype(np.float32)
CH, CW = comp.shape[:2]
MH, MW = mod.shape[:2]
solid = comp[..., 3] > 200


SOLID_YS, SOLID_XS = np.nonzero(solid)


def err(dx, dy, sample=None):
    """合成图左上角放在原图 (dx, dy) 处，只比合成图不透明且落在原图内的像素（可只取一部分采样点）。"""
    ys, xs = (SOLID_YS, SOLID_XS) if sample is None else (SOLID_YS[sample], SOLID_XS[sample])
    my, mx = ys + dy, xs + dx
    ok = (mx >= 0) & (my >= 0) & (mx < MW) & (my < MH)
    if ok.sum() < len(ys) * 0.6:
        return 1e9
    return float(np.abs(comp[ys[ok], xs[ok]] - mod[my[ok], mx[ok]]).mean())


# 粗搜：步长 8、只用 1500 个采样点；再在最优点附近用全部不透明像素逐像素细搜
rng = np.random.default_rng(0)
sample = rng.choice(len(SOLID_YS), size=min(1500, len(SOLID_YS)), replace=False)
best = (1e9, 0, 0)
step = 8
for dy in range(-CH // 2, MH - CH // 2, step):
    for dx in range(-CW // 2, MW - CW // 2, step):
        e = err(dx, dy, sample)
        if e < best[0]:
            best = (e, dx, dy)
_, bx, by = best
best = (1e9, bx, by)
for dy in range(by - step, by + step + 1):
    for dx in range(bx - step, bx + step + 1):
        e = err(dx, dy)
        if e < best[0]:
            best = (e, dx, dy)
e, dx, dy = best
# 合成图放到原图坐标后，与原图差别大的像素算特效
placed = np.zeros_like(mod)
y0, y1 = max(dy, 0), min(dy + CH, MH)
x0, x1 = max(dx, 0), min(dx + CW, MW)
placed[y0:y1, x0:x1] = comp[y0 - dy:y1 - dy, x0 - dx:x1 - dx]
# 模组原图与原版合成常有轻微缩放或亚像素偏差，人物边缘的颜色差会被当成特效：
# 把原版人物区域外扩 GROW 像素整块排除，只留人物以外的部分（贴着人物的一小段特效会被切掉）
GROW = 14
from PIL import ImageFilter  # noqa: E402

body = Image.fromarray(((placed[..., 3] > 20) * 255).astype(np.uint8), "L").filter(ImageFilter.MaxFilter(2 * GROW + 1))
fx_mask = np.asarray(body) == 0
fx = mod.copy()
fx[..., 3] = np.where(fx_mask, mod[..., 3], 0)
fx_img = Image.fromarray(fx.astype(np.uint8), "RGBA")
box = fx_img.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
fx_img = fx_img.crop(box)
fx_img.save(out)
ox, oy = info["composite_origin"]
# 原图像素 (px, py) → 合成图像素 (px - dx, py - dy) → 原版坐标 (x - ox, oy - y)
cx = (box[0] + box[2]) / 2 - dx - ox
cy = oy - ((box[1] + box[3]) / 2 - dy)
print(json.dumps({"err": round(e, 2), "offset": [dx, dy], "size": list(fx_img.size), "center": [round(cx, 1), round(cy, 1)]}))
