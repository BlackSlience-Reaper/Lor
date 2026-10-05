"""给原版所有人物包里每个动作的合成图算缩略特征（裁到可见范围、缩成 32×32 的 alpha 与灰度 + 宽高比），
再给模组 images/monsters 下每张贴图算同样的特征，找最像的原版动作。用来查哪些模组怪物在原版里有分层素材。
用法：signatures.py build <输出.npz>            —— 扫原版
      signatures.py match <原版.npz> <输出.json> —— 对模组贴图"""
import json
import sys
from pathlib import Path

import numpy as np
import UnityPy
from PIL import Image

G = Path.home() / ".local/share/LibraryOfRuina-win/LibraryOfRuina_Data/StreamingAssets/AssetBundles/char"
R = Path("/Users/iniad/LibraryOfRuina")
N = 32


def pid(p):
    return getattr(p, "path_id", None) or getattr(p, "m_PathID", 0)


def signature(im):
    im = im.convert("RGBA")
    box = im.getchannel("A").point(lambda v: 255 if v > 40 else 0).getbbox()
    if not box:
        return None
    im = im.crop(box)
    w, h = im.size
    small = im.resize((N, N), Image.BILINEAR)
    a = np.asarray(small.getchannel("A"), np.float32) / 255
    g = np.asarray(small.convert("L"), np.float32) / 255 * a
    return np.concatenate([a.ravel(), g.ravel()]), w / h, max(w, h)


def world(by_id, tr):
    x = y = 0.0
    t = tr.read()
    while True:
        x += t.m_LocalPosition.x
        y += t.m_LocalPosition.y
        f = pid(t.m_Father)
        if not f:
            break
        t = by_id[f].read()
    return x * 100, y * 100


def build(dst):
    sigs, meta = [], []
    for f in sorted(G.glob("*.pres"), key=lambda p: int(p.stem.split("_")[1])):
        try:
            env = UnityPy.load(str(f))
        except Exception:
            continue
        by_id = {o.path_id: o for o in env.objects}
        prefab = next(iter(env.container.keys()), f.stem)
        for o in env.objects:
            if o.type.name != "MonoBehaviour":
                continue
            try:
                mb = o.read()
                s = by_id.get(pid(mb.m_Script))
                if not s or s.read().m_ClassName != "CharacterMotion":
                    continue
                tree = o.read_typetree()
                motion = by_id[pid(mb.m_GameObject)].read().m_Name
                placed = []
                for ss in tree.get("motionSpriteSet", []):
                    sr = by_id[ss["sprRenderer"]["m_PathID"]].read()
                    sp = by_id.get(pid(sr.m_Sprite))
                    go = by_id[pid(sr.m_GameObject)].read()
                    if sp is None or not sr.m_Enabled or not go.m_IsActive:
                        continue
                    spr = sp.read()
                    img = spr.image
                    if img.width == 0 or img.height == 0:
                        continue
                    tr = next(by_id[pid(c.component if hasattr(c, "component") else c)] for c in go.m_Components
                              if by_id[pid(c.component if hasattr(c, "component") else c)].type.name == "Transform")
                    x, y = world(by_id, tr)
                    w, h = img.size
                    placed.append((sr.m_SortingOrder, img, x - spr.m_Pivot.x * w, y + (1 - spr.m_Pivot.y) * h,
                                   ss["sprType"]))
                if not placed:
                    continue
                minx = min(p[2] for p in placed)
                maxy = max(p[3] for p in placed)
                W = round(max(p[2] + p[1].width for p in placed) - minx) + 2
                H = round(maxy - min(p[3] - p[1].height for p in placed)) + 2
                if W * H > 6000 * 6000:
                    continue
                canvas = Image.new("RGBA", (W, H))
                for _, img, left, top, _ in sorted(placed, key=lambda p: p[0]):
                    canvas.alpha_composite(img.convert("RGBA"), (round(left - minx), round(maxy - top)))
                sg = signature(canvas)
                if sg is None:
                    continue
                sigs.append(sg[0])
                meta.append({"bundle": f.stem, "prefab": prefab, "motion": motion, "layers": len(placed),
                             "types": sorted({p[4] for p in placed}), "aspect": sg[1], "size": sg[2]})
            except Exception as e:
                print("skip", f.stem, e, flush=True)
        print(f.stem, len(meta), flush=True)
    np.savez_compressed(dst, sigs=np.stack(sigs), meta=json.dumps(meta))


def match(src, dst):
    data = np.load(src)
    sigs = data["sigs"]
    meta = json.loads(str(data["meta"]))
    aspects = np.array([m["aspect"] for m in meta])
    sizes = np.array([m["size"] for m in meta])
    out = {}
    for p in sorted((R / "images/monsters").rglob("*")):
        if p.suffix.lower() not in (".png", ".webp") or "spine" in p.stem or p.stem.startswith("guest_"):
            continue
        try:
            sg = signature(Image.open(p))
        except Exception:
            continue
        if sg is None:
            continue
        v, asp, size = sg
        d = np.abs(sigs - v).mean(axis=1) + 0.3 * np.abs(np.log(aspects / asp)) + 0.1 * np.abs(np.log(sizes / size))
        order = np.argsort(d)[:3]
        out[str(p.relative_to(R / "images/monsters"))] = [{**meta[i], "dist": round(float(d[i]), 4)} for i in order]
    Path(dst).write_text(json.dumps(out, ensure_ascii=False, indent=1))
    print(len(out))


if __name__ == "__main__":
    if sys.argv[1] == "build":
        build(sys.argv[2])
    else:
        match(sys.argv[2], sys.argv[3])
