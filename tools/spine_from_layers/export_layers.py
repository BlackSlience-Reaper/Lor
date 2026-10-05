"""按动作导出人物分层：<out>/<动作>/<层>.png，layers.json 记录每层在角色根坐标下的位置（像素，y 向上）、
枢轴、排序；同时把每个动作按排序合成成一张整图 <out>/<动作>.composite.png，用来与模组现有贴图核对。
用法：python3 export_layers.py char_460 out_dir"""
import json
import math
import sys
from pathlib import Path

import UnityPy
from PIL import Image

G = Path.home() / ".local/share/LibraryOfRuina-win/LibraryOfRuina_Data/StreamingAssets/AssetBundles/char"
name, out = sys.argv[1], Path(sys.argv[2])
env = UnityPy.load(str(G / f"{name}.pres"))
by_id = {o.path_id: o for o in env.objects}


def pid(p):
    return getattr(p, "path_id", None) or getattr(p, "m_PathID", 0)


def local(t):
    q = t.m_LocalRotation
    return (t.m_LocalPosition.x, t.m_LocalPosition.y, math.degrees(2 * math.atan2(q.z, q.w)),
            t.m_LocalScale.x, t.m_LocalScale.y)


def world(tr_obj):
    """沿父链合成 2D 变换，返回 (x, y, 旋转度, sx, sy)；只处理绕 z 轴的旋转（2D 立绘足够）。"""
    chain = []
    t = tr_obj.read()
    while True:
        chain.append(local(t))
        f = pid(t.m_Father)
        if not f:
            break
        t = by_id[f].read()
    x = y = rot = 0.0
    sx = sy = 1.0
    for lx, ly, lr, lsx, lsy in reversed(chain):
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        px, py = lx * sx, ly * sy
        x, y = x + c * px - s * py, y + s * px + c * py
        rot += lr
        sx, sy = sx * lsx, sy * lsy
    return x, y, rot, sx, sy


result = {}
for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    mb = obj.read()
    script = by_id.get(pid(mb.m_Script))
    if not script or script.read().m_ClassName != "CharacterMotion":
        continue
    tree = obj.read_typetree()
    go = by_id[pid(mb.m_GameObject)].read()
    motion = go.m_Name
    layers = []
    d = out / motion
    d.mkdir(parents=True, exist_ok=True)
    for s in tree["motionSpriteSet"]:
        sr_obj = by_id[s["sprRenderer"]["m_PathID"]]
        sr = sr_obj.read()
        lgo = by_id[pid(sr.m_GameObject)].read()
        tr_obj = next(by_id[pid(c.component if hasattr(c, "component") else c)] for c in lgo.m_Components
                      if by_id[pid(c.component if hasattr(c, "component") else c)].type.name == "Transform")
        tr = tr_obj.read()
        sobj = by_id.get(pid(sr.m_Sprite))
        if sobj is None:
            continue
        spr = sobj.read()
        img = spr.image
        if img.width == 0 or img.height == 0 or img.getchannel("A").getbbox() is None:
            continue
        fname = lgo.m_Name.replace(" ", "_")
        taken = {l["name"] for l in layers}
        k = 2
        while fname in taken:
            fname = f"{lgo.m_Name.replace(' ', '_')}~{k}"
            k += 1
        if bool(sr.m_FlipX):
            img = img.transpose(Image.FLIP_LEFT_RIGHT)
        if bool(sr.m_FlipY):
            img = img.transpose(Image.FLIP_TOP_BOTTOM)
        img.save(d / f"{fname}.png")
        wx, wy, rot, wsx, wsy = world(tr_obj)
        layers.append({"name": fname, "type": s["sprType"], "order": sr.m_SortingOrder, "enabled": bool(sr.m_Enabled),
                       "active": bool(lgo.m_IsActive), "pos": [round(wx * 100, 2), round(wy * 100, 2)],
                       "rot": round(rot, 3), "scale": [round(wsx, 4), round(wsy, 4)],
                       "size": list(img.size),
                       "pivot": [1 - spr.m_Pivot.x if sr.m_FlipX else spr.m_Pivot.x, 1 - spr.m_Pivot.y if sr.m_FlipY else spr.m_Pivot.y],
                       "ppu": spr.m_PixelsToUnits})
    layers.sort(key=lambda l: l["order"])
    result[motion] = layers

def effective_scale(l):
    """层贴图在角色坐标里的实际缩放：世界缩放 × 100 / 每单位像素。原版大多是 100，薄暝的 F/S3、赤瞳的红眼茧、
    喵呜的突刺等是 50，同样像素的图要画大一倍；坐标统一按 1 单位 = 100 像素。"""
    k = 100.0 / (l.get("ppu") or 100.0)
    return l["scale"][0] * k, l["scale"][1] * k


def placed(l):
    """层贴图按世界缩放、旋转变换后的图与其中心（角色根坐标，y 向上）。"""
    w, h = l["size"]
    sx, sy = effective_scale(l)
    r = math.radians(l["rot"])
    ox, oy = (0.5 - l["pivot"][0]) * w * sx, (0.5 - l["pivot"][1]) * h * sy
    cx = l["pos"][0] + math.cos(r) * ox - math.sin(r) * oy
    cy = l["pos"][1] + math.sin(r) * ox + math.cos(r) * oy
    im = Image.open(out / l["motion"] / f"{l['name']}.png").convert("RGBA")
    if abs(abs(sx) - 1) > 1e-3 or abs(abs(sy) - 1) > 1e-3:
        im = im.resize((max(1, round(w * abs(sx))), max(1, round(h * abs(sy)))), Image.LANCZOS)
    if sx < 0:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    if sy < 0:
        im = im.transpose(Image.FLIP_TOP_BOTTOM)
    if abs(l["rot"]) > 1e-3:
        im = im.rotate(l["rot"], resample=Image.BICUBIC, expand=True)
    return im, cx, cy


# 合成：统一画布；去掉未启用的层；记录每层中心（生成骨骼时用中心 + 旋转 + 缩放放附件）
for motion in list(result):
    layers = [l for l in result[motion] if l["enabled"] and l["active"]]
    placed_list = []
    for l in layers:
        l["motion"] = motion
        im, cx, cy = placed(l)
        l["center"] = [round(cx, 2), round(cy, 2)]
        placed_list.append((im, cx, cy))
    xs = [cx + s * im.width / 2 for im, cx, cy in placed_list for s in (-1, 1)]
    ys = [cy + s * im.height / 2 for im, cx, cy in placed_list for s in (-1, 1)]
    minx, maxy = min(xs), max(ys)
    W, H = round(max(xs) - minx) + 2, round(maxy - min(ys)) + 2
    canvas = Image.new("RGBA", (W, H))
    for im, cx, cy in placed_list:
        canvas.alpha_composite(im, (round(cx - im.width / 2 - minx), round(maxy - cy - im.height / 2)))
    canvas.save(out / f"{motion}.composite.png")
    result[motion] = {"layers": layers, "composite_origin": [round(-minx, 2), round(maxy, 2)]}
(out / "layers.json").write_text(json.dumps(result, ensure_ascii=False, indent=1))
for m, v in result.items():
    print(m, [(l["name"], l["order"], l["size"], l["pos"]) for l in v["layers"]])
