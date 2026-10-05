"""按统一规则给来宾生成 spine_from_layers 配置（guest_<名字>.json）。

规则：
- 姿势：待机用原版 Default；打击/突刺/斩击/受击用模组原图对上的原版动作（match_all.py 的结果），
  没有原图的（芬恩、不莱梅乐队）用原版 Hit/Penetrate/Slash/Damaged。
- 骨头（每个姿势一组）：body 在脚底（角色根 y=0，x 取脖子），head 在脖子（头层下沿往后一点），
  hair 在头顶附近，前发、后发挂 hair；头、脸、兜帽、面具挂 head；其余（身体、皮肤、特效）挂 body。
- 武器：待机姿势里单独成层的武器（OVERRIDES 里列名字），以离它最近的手（皮肤层）为轴挂 weapon 骨头，
  那只手一起挂上，绕握把转时手不动、武器摆；蓄力时把武器尖端往上、往后抡。
- 动画参数同艾莉：打击往前冲、突刺/斩击原地小幅前送，受击后退泛红，死亡原地低头下沉。
用法：build_guest_configs.py <match.json> <分层根目录> <输出目录> [名字...]
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

HEAD_TYPES = {4, 5, 6, 7}  # 脸、头、兜帽、面具
HAIR_TYPES = {0, 1}  # 前发、后发
SKIN = 3
# 待机姿势里单独成层、可以绕手转的武器层；None 表示没有（武器和身体画在一起）
WEAPONS = {
    "yun": None, "philip": ["body_"], "salvador": ["body", "body_5"], "yuna": ["body"], "oscar": ["body_2"],
    "pamela": ["body_7"], "pameli": ["body"], "gin": None, "yang": ["body_4"], "sayo": ["body_4"],
    "mccullin": ["body_2"], "naoki": None, "taein": ["1", "3_1"], "arnold": ["2"], "consta": ["1"], "mo": ["body"],
    "finn": None, "meow": ["body_10"], "mu_mu": ["body"], "oink": ["body_2"],
}
FALLBACK = {"strike": "Hit", "thrust": "Penetrate", "slash": "Slash", "hurt": "Damaged"}


def bbox(root, motion, l):
    """层可见像素的包围盒（角色根坐标，y 向上）：x0, y0, x1, y1。"""
    im = Image.open(root / motion / f"{l['name']}.png")
    b = im.getchannel("A").getbbox()
    w, h = l["size"]
    cx, cy = l.get("center", l["pos"])
    left, top = cx - w / 2, cy + h / 2
    return (left + b[0], top - b[3], left + b[2], top - b[1])


def tip_point(root, motion, l, pivot):
    """武器可见像素里离握把最远的点。"""
    im = np.asarray(Image.open(root / motion / f"{l['name']}.png").getchannel("A"))
    ys, xs = np.nonzero(im > 128)
    w, h = l["size"]
    cx, cy = l.get("center", l["pos"])
    px = cx - w / 2 + xs
    py = cy + h / 2 - ys
    d = (px - pivot[0]) ** 2 + (py - pivot[1]) ** 2
    i = int(d.argmax())
    return float(px[i]), float(py[i])


def opaque(root, motion, l):
    """层的不透明掩码与 RGB（按实际缩放），以及左上角在角色坐标里的位置（y 向上）。"""
    im = Image.open(root / motion / f"{l['name']}.png").convert("RGBA")
    k = 100.0 / (l.get("ppu") or 100.0)
    sx, sy = abs(l["scale"][0] * k), abs(l["scale"][1] * k)
    if abs(sx - 1) > 1e-3 or abs(sy - 1) > 1e-3:
        im = im.resize((max(1, round(im.width * sx)), max(1, round(im.height * sy))))
    a = np.asarray(im).astype(np.float32)
    cx, cy = l.get("center", l["pos"])
    return a, round(cx - im.width / 2), round(cy + im.height / 2)


def covered(root, motion, a, b, cache):
    """a 的不透明像素有多少落在 b 的不透明像素上，以及重合处的平均色差。"""
    pa_full, ax, ay = cache.setdefault(a["name"], opaque(root, motion, a))
    pb_full, bx, by = cache.setdefault(b["name"], opaque(root, motion, b))
    x0, x1 = max(ax, bx), min(ax + pa_full.shape[1], bx + pb_full.shape[1])
    y1, y0 = min(ay, by), max(ay - pa_full.shape[0], by - pb_full.shape[0])
    total = (pa_full[..., 3] > 128).sum()
    if x1 <= x0 or y1 <= y0 or total == 0:
        return 0.0, 255.0
    pa = pa_full[ay - y1:ay - y0, x0 - ax:x1 - ax]
    pb = pb_full[by - y1:by - y0, x0 - bx:x1 - bx]
    both = (pa[..., 3] > 128) & (pb[..., 3] > 128)
    if not both.any():
        return 0.0, 255.0
    return both.sum() / total, float(np.abs(pa[both][:, :3] - pb[both][:, :3]).mean())


def duplicate_of(root, motion, l, others, cache, max_diff=25.0):
    """原版有的东西画了两份、静止时完全重合（例如类型 8 的“司书头饰”和头发层里各一顶皇冠）。返回与 l 重复的那层。"""
    best = None
    for o in others:
        if o is l:
            continue
        frac, diff = covered(root, motion, l, o, cache)
        if frac >= 0.5 and diff < max_diff and (best is None or frac > best[0]):
            best = (frac, o)
    return best[1] if best else None


def rig_motion(root, motion, layers, weapon_names=None):
    heads = [l for l in layers if l["type"] == 5] or [l for l in layers if l["type"] in HEAD_TYPES]
    hb = [bbox(root, motion, l) for l in heads]
    if not hb:
        # 整图怪物（暗影狼、拟态第二形态）没有头层：取全部层包围盒的上四分之一当头，头骨只用来定转轴
        ab = [bbox(root, motion, l) for l in layers]
        top, bottom = max(b[3] for b in ab), min(b[1] for b in ab)
        hb = [(min(b[0] for b in ab), top - (top - bottom) / 4, max(b[2] for b in ab), top)]
    x0, y0, x1, y1 =min(b[0] for b in hb), min(b[1] for b in hb), max(b[2] for b in hb), max(b[3] for b in hb)
    hw, hh = x1 - x0, y1 - y0
    neck = [round((x0 + x1) / 2 + 0.07 * hw, 1), round(y0 + 0.05 * hh, 1)]
    hair = [round((x0 + x1) / 2, 1), round(y0 + 0.75 * hh, 1)]
    bones = {"body": {"at": [neck[0], 0.0]}, "head": {"parent": "body", "at": neck},
             "hair": {"parent": "head", "at": hair}}
    assign = {}
    for l in layers:
        t = l["type"]
        assign[l["name"]] = "head" if t in HEAD_TYPES else "hair" if t in HAIR_TYPES else "body"
    weapon = None
    if weapon_names:
        wl = [l for l in layers if l["name"] in weapon_names]
        hands = [l for l in layers if l["type"] == SKIN]
        if wl and hands:
            wb = [bbox(root, motion, l) for l in wl]
            wx0, wy0, wx1, wy1 = min(b[0] for b in wb), min(b[1] for b in wb), max(b[2] for b in wb), max(b[3] for b in wb)

            def dist(h):
                hx0, hy0, hx1, hy1 = bbox(root, motion, h)
                cx, cy = (hx0 + hx1) / 2, (hy0 + hy1) / 2
                dx = max(wx0 - cx, 0, cx - wx1)
                dy = max(wy0 - cy, 0, cy - wy1)
                return math.hypot(dx, dy), (cx, cy)

            ranked = sorted((dist(h) + (h,) for h in hands), key=lambda v: v[0])
            grip_d, grip, hand = ranked[0]
            if grip_d < 40:
                bones["weapon"] = {"parent": "body", "at": [round(grip[0], 1), round(grip[1], 1)]}
                for l in wl:
                    assign[l["name"]] = "weapon"
                for d, c, h in ranked:
                    if d < 40 and math.hypot(c[0] - grip[0], c[1] - grip[1]) < 90:
                        assign[h["name"]] = "weapon"
                tip = tip_point(root, motion, max(wl, key=lambda l: l["size"][0] * l["size"][1]), grip)
                weapon = {"grip": grip, "tip": tip}
    # 两份重合的东西挂在不同骨头上，动起来就会错开成重影，所以重复的那份跟着原件走
    cache = {}
    # 原版常把同一件饰物画两份（类型 8 的“司书头饰”和头发/脸/背饰层里各一份，类型标注也不统一），静止时完全重合。
    # 两份挂在不同骨头上，动起来就错开成重影，所以每组重复层统一挂一根骨头：组里有头或头发上的层，跟面积最大的那层；
    # 否则都挂身体。没有重复的类型 8 层几乎都是头饰，挂到头上。
    group = {l["name"]: l["name"] for l in layers}

    def find(n):
        while group[n] != n:
            n = group[n]
        return n

    for i, la in enumerate(layers):
        for lb in layers[i + 1:]:
            frac, diff = covered(root, motion, la, lb, cache)
            frac_b, _ = covered(root, motion, lb, la, cache)
            if max(frac, frac_b) >= 0.5 and diff < 12:
                group[find(la["name"])] = find(lb["name"])
    members = {}
    for l in layers:
        members.setdefault(find(l["name"]), []).append(l)
    for ms in members.values():
        if len(ms) == 1:
            l = ms[0]
            if l["type"] == 8:
                # 整块压在头发或头上的饰物（发带、发卡）跟着那层走，免得头发甩动时滑开
                hosts = [(covered(root, motion, l, o, cache)[0], o) for o in layers
                         if o is not l and o["type"] != 8 and assign[o["name"]] in ("head", "hair")]
                hosts = [h for h in hosts if h[0] >= 0.9]
                assign[l["name"]] = assign[max(hosts, key=lambda h: h[0])[1]["name"]] if hosts else "head"
            continue
        movers = [m for m in ms if assign[m["name"]] in ("head", "hair") and m["type"] != 8]
        target = assign[max(movers, key=lambda m: m["size"][0] * m["size"][1])["name"]] if movers else "body"
        for m in ms:
            if assign[m["name"]] != "weapon":
                assign[m["name"]] = target
    if weapon is not None:
        # 整块压在武器上的身体层（尤娜琴盒上的背带、握着琴盒的手套）跟着武器走
        weapon_layers = [l for l in layers if assign[l["name"]] == "weapon"]
        for l in layers:
            if assign[l["name"]] == "body" and any(covered(root, motion, l, w, cache)[0] >= 0.9 for w in weapon_layers):
                assign[l["name"]] = "weapon"
        # 握把旁的手：皮肤层若还画着身体其它部位（沙耶的这层连着两条腿），不能跟着武器转
        body_layers = [l for l in layers if assign[l["name"]] == "body"]
        for l in layers:
            if (assign[l["name"]] == "weapon" and l["type"] == SKIN and l["name"] not in (weapon_names or [])
                    and duplicate_of(root, motion, l, body_layers, cache, max_diff=12.0) is not None):
                assign[l["name"]] = "body"
    return bones, assign, weapon


def raise_sign(weapon):
    """蓄力时武器的旋转方向。偏水平的武器把尖端往上抬：尖端在握把左边顺时针（负），右边逆时针（正）；
    偏竖直的武器往后（+x，背对朝向）倒：尖端朝上顺时针，朝下逆时针。"""
    vx = weapon["tip"][0] - weapon["grip"][0]
    vy = weapon["tip"][1] - weapon["grip"][1]
    if abs(vx) >= abs(vy):
        return 1.0 if vx > 0 else -1.0
    return -1.0 if vy > 0 else 1.0


def animations(poses, weapon):
    D = poses["idle"]
    k = {}
    sign = raise_sign(weapon) if weapon else 0.0

    def windup(extra):
        keys = {f"{D}.body": {"rotate": [[0, 0], [0.15, -4, "out"], [0.7, -4], [0.95, 0]]},
                f"{D}.head": {"rotate": [[0, 0], [0.15, 3, "out"], [0.7, 0], [1.0, 0]]}}
        if weapon:
            keys[f"{D}.weapon"] = {"rotate": [[0, 0], [0.15, sign * extra, "out"], [0.7, sign * extra], [0.95, 0]]}
        return keys

    def lag(pose):
        return {f"{pose}.head": {"rotate": [[0.15, -5], [0.32, 4, "out"], [0.55, 0]]},
                f"{pose}.hair": {"rotate": [[0.15, -4], [0.36, 5, "out"], [0.6, 0]]}}

    idle_osc = {f"{D}.body": {"sy": [0.009, 1, 0.0]}, f"{D}.head": {"rotate": [2.0, 1, 0.15]},
                f"{D}.hair": {"rotate": [1.4, 1, 0.4]}}
    if weapon:
        idle_osc[f"{D}.weapon"] = {"rotate": [1.2, 1, 0.3]}
    k["idle"] = {"duration": 2.4, "show": [[0, D, 0]], "osc": idle_osc}

    s = poses["strike"]
    k["strike"] = {"duration": 1.0, "show": [[0, D, 0], [0.15, s, 0.05], [0.78, D, 0.08]],
                   "keys": {"move": {"x": [[0, 0], [0.15, 0], [0.3, -60, "out"], [0.75, -60], [0.95, 0]]},
                            **windup(20),
                            f"{s}.body": {"rotate": [[0.15, -3], [0.3, 2, "out"], [0.5, 0]],
                                          "sx": [[0.15, 1], [0.3, 1.03, "out"], [0.45, 1]],
                                          "sy": [[0.15, 1], [0.3, 0.97, "out"], [0.45, 1]]},
                            **lag(s)}}
    t = poses["thrust"]
    th = windup(6)
    th[f"{D}.body"]["x"] = [[0, 0], [0.15, 12, "out"], [0.7, 12], [0.95, 0]]
    k["thrust"] = {"duration": 1.0, "show": [[0, D, 0], [0.15, t, 0.05], [0.78, D, 0.08]],
                   "keys": {**th, f"{t}.body": {"x": [[0.15, 12], [0.3, -14, "out"], [0.55, 0]]}, **lag(t)}}
    sl = poses["slash"]
    k["slash"] = {"duration": 1.0, "show": [[0, D, 0], [0.15, sl, 0.05], [0.78, D, 0.08]],
                  "keys": {**windup(32),
                           f"{sl}.body": {"rotate": [[0.15, -3], [0.3, 3, "out"], [0.55, 0]],
                                          "y": [[0.15, 0], [0.3, -8, "out"], [0.55, 0]]},
                           **lag(sl)}}
    h = poses["hurt"]
    k["hurt"] = {"duration": 0.6, "show": [[0, h, 0.04], [0.42, D, 0.1]], "flash": [0.0, 0.22, [1.0, 0.6, 0.6]],
                 "keys": {"move": {"x": [[0, 0], [0.1, 20, "out"], [0.5, 0]]},
                          f"{h}.body": {"rotate": [[0, 0], [0.08, -6, "out"], [0.4, 0]]},
                          f"{h}.head": {"rotate": [[0, 0], [0.1, -8, "out"], [0.45, 0]]},
                          f"{h}.hair": {"rotate": [[0, 0], [0.14, -10, "out"], [0.5, 0]]}}}
    die = {f"{D}.body": {"rotate": [[0, 0], [0.9, 3, "out"], [1.2, 3]], "sy": [[0, 1], [0.9, 0.96, "out"], [1.2, 0.96]]},
           f"{D}.head": {"rotate": [[0, 0], [0.8, 16, "out"], [1.2, 16]]},
           f"{D}.hair": {"rotate": [[0, 0], [0.9, 8, "out"], [1.2, 8]]}}
    if weapon:
        die[f"{D}.weapon"] = {"rotate": [[0, 0], [0.7, -sign * 5, "out"], [1.2, -sign * 5]]}
    k["die"] = {"duration": 1.2, "show": [[0, D, 0]], "keys": die}
    return k


def main():
    match = json.loads(Path(sys.argv[1]).read_text())
    root = Path(sys.argv[2]).expanduser()
    out = Path(sys.argv[3])
    only = set(sys.argv[4:])
    for name, m in match.items():
        if only and name not in only:
            continue
        sub = m["layers"]
        info = json.loads((root / sub / "layers.json").read_text())
        prefix = next(k for k in info if k.endswith("_Default"))[: -len("Default")]
        # 待机姿势照模组待机图对上的动作（铁之兄弟的莫用的是原版防御姿势）
        poses = {"idle": m["poses"]["idle"]["motion"][len(prefix):]}
        poses.update({kind: m["poses"][kind]["motion"][len(prefix):] if kind in m["poses"] else FALLBACK[kind]
                      for kind in FALLBACK})
        base = poses["idle"]
        motions = {}
        weapon = None
        for pose in [base] + sorted(set(poses.values()) - {base}):
            layers = info[prefix + pose]["layers"]
            bones, assign, wp = rig_motion(root / sub, prefix + pose, layers,
                                           WEAPONS.get(name) if pose == base else None)
            if pose == base:
                weapon = wp
            motions[pose] = {"source": prefix + pose, "bones": bones, "assign": assign}
        cfg = {"name": f"guest_{name}", "layers_dir": f"~/.local/share/LibraryOfRuina-layers/{sub}",
               "origin": m["poses"]["idle"]["bottom_center"], "skin_tint": m.get("skin_tint", [0.85, 0.82, 0.79]),
               "fps": 30, "setup_motion": base, "motions": motions, "animations": animations(poses, weapon)}
        (out / f"guest_{name}.json").write_text(json.dumps(cfg, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
        print(name, poses, "weapon" if weapon else "-", weapon)


if __name__ == "__main__":
    main()
