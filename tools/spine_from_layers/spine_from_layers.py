"""用原版分层素材生成 Spine 骨骼（区域附件，不做网格变形）。

原版来宾每个动作（待机、打击、突刺、斩击、受伤……）是一组独立的 SpriteRenderer：身体、头、脸、前发、
皮肤层（白底贴图，运行时乘肤色）等，各层位置写在预制体里。这里每个动作建一组骨头、一组槽位，
动画里到点瞬间换成另一个动作的整组附件（与原版一样，不做逐层淡入淡出），骨头负责呼吸、点头、蓄力等动作。

坐标：配置里的点都是原版角色根坐标（像素，y 向上，100 像素 = 1 个 Unity 单位）。
`origin` 是骨架原点在这个坐标系里的位置；游戏里骨架原点对齐待机贴图的底边中点（见 RuntimeSpineBody），
所以 `origin` 要填模组待机图底边中点在原版坐标里的位置（tools/spine_from_layers/match_mod.py 可算）。

用法：python3 spine_from_layers.py <config.json> [--out 目录]
产物：<name>.atlas、<name>.spine-json、<name>.webp（所有层打成一页）。
"""
import argparse
import json
import math
from pathlib import Path

from PIL import Image

# ---------------------------------------------------------------- 素材


def load_layers(cfg):
    root = Path(cfg["layers_dir"]).expanduser()
    info = json.loads((root / "layers.json").read_text())
    tint = cfg.get("skin_tint")
    motions = {}
    for motion, m in cfg["motions"].items():
        src = info[m["source"]]["layers"]
        layers = []
        for l in src:
            im = Image.open(root / m["source"] / f"{l['name']}.png").convert("RGBA")
            if l["type"] == 3 and tint:
                r, g, b, a = im.split()
                r = r.point(lambda v: round(v * tint[0]))
                g = g.point(lambda v: round(v * tint[1]))
                b = b.point(lambda v: round(v * tint[2]))
                im = Image.merge("RGBA", (r, g, b, a))
            w, h = l["size"]
            cx = l["pos"][0] + (0.5 - l["pivot"][0]) * w
            cy = l["pos"][1] + (0.5 - l["pivot"][1]) * h
            # 裁掉透明边，附件中心随之挪到裁剪框中心（原版切片常带大片空白，攻击图尤甚）
            box = im.getchannel("A").getbbox()
            if box:
                x0, y0, x1, y1 = box
                cx += (x0 + x1) / 2 - w / 2
                cy -= (y0 + y1) / 2 - h / 2
                im = im.crop(box)
            layers.append({"name": l["name"], "image": im, "center": (cx, cy), "order": l["order"]})
        # 额外层：原版里单独播放、模组原图上叠着的特效（extract_fx.py 抠出），路径相对配置文件
        for ex in m.get("extra", []):
            im = Image.open(cfg["_dir"] / ex["image"]).convert("RGBA")
            layers.append({"name": ex["name"], "image": im, "center": tuple(ex["center"]), "order": ex.get("order", 999)})
        layers.sort(key=lambda l: l["order"])
        motions[motion] = layers
    return motions


def pack(images, max_w=2048, max_h=4096, pad=2):
    """按高度排序的行打包，一页放不下就开新页（页高不超过 max_h：部分显卡纹理上限 8192，大页也占显存）。
    返回 {key: (页号, x, y)} 与各页尺寸。"""
    items = sorted(images.items(), key=lambda kv: -kv[1].height)
    pages = [[0, 0]]
    x = y = row_h = 0
    pos = {}
    for key, im in items:
        if x + im.width + pad > max_w:
            x = 0
            y += row_h + pad
            row_h = 0
        if y + im.height > max_h and (x > 0 or y > 0):
            pages.append([0, 0])
            x = y = row_h = 0
        pos[key] = (len(pages) - 1, x, y)
        x += im.width + pad
        row_h = max(row_h, im.height)
        pages[-1][0] = max(pages[-1][0], x)
        pages[-1][1] = max(pages[-1][1], y + row_h)
    return pos, pages


# ---------------------------------------------------------------- 动画曲线

EASE = {
    "linear": lambda u: u,
    "smooth": lambda u: u * u * (3 - 2 * u),
    "out": lambda u: 1 - (1 - u) ** 3,
    "in": lambda u: u ** 3,
    # 冲过头再回落，幅度约 8%
    "back": lambda u: 1 + 2.2 * (u - 1) ** 3 + 1.2 * (u - 1) ** 2,
}


def eval_keys(keys, t):
    """keys = [[时间, 值, 缓动(到达这个关键帧时用)], ...]；值可以是数或 [x, y]。"""
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, v0, *_), (t1, v1, *e) in zip(keys, keys[1:]):
        if t <= t1:
            u = 0.0 if t1 == t0 else (t - t0) / (t1 - t0)
            f = EASE[e[0] if e else "smooth"](u)
            if isinstance(v0, list):
                return [a + (b - a) * f for a, b in zip(v0, v1)]
            return v0 + (v1 - v0) * f
    return keys[-1][1]


def osc(spec, t, duration):
    """spec = [振幅, 每个循环的周期数, 相位(0-1)]，循环动画里首尾相接。"""
    amp, cycles, phase = spec
    return amp * math.sin(2 * math.pi * (cycles * t / duration + phase))


# ---------------------------------------------------------------- 主流程


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("config")
    ap.add_argument("--out")
    args = ap.parse_args()
    cfg_path = Path(args.config).resolve()
    cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
    cfg["_dir"] = cfg_path.parent
    out = Path(args.out) if args.out else cfg_path.parent / "out"
    out.mkdir(parents=True, exist_ok=True)
    name = cfg["name"]
    fps = cfg.get("fps", 30)
    shift = (-cfg["origin"][0], -cfg["origin"][1])
    setup_motion = cfg["setup_motion"]
    motions = load_layers(cfg)

    # 骨头：root → move（整体位移，冲刺、击退）→ 各动作的骨头
    # root_scale：模组贴图是原版按某倍数缩放过的（翅振 1.2 倍）时整体放大，骨架才与模组待机图对齐
    rs = cfg.get("root_scale", 1.0)
    bones = [{"name": "root", **({"scaleX": rs, "scaleY": rs} if rs != 1.0 else {})}, {"name": "move", "parent": "root"}]
    world = {"root": (0.0, 0.0), "move": (0.0, 0.0)}
    for motion, m in cfg["motions"].items():
        for role, b in m["bones"].items():
            bname = f"{motion}_{role}"
            parent = f"{motion}_{b['parent']}" if b.get("parent") else "move"
            wx, wy = b["at"][0] + shift[0], b["at"][1] + shift[1]
            px, py = world[parent]
            bones.append({"name": bname, "parent": parent, "x": round(wx - px, 2), "y": round(wy - py, 2)})
            world[bname] = (wx, wy)

    # 图集
    # texture_scale < 1 时贴图按比例缩小存放，附件仍按原尺寸绘制（运行库按 附件尺寸/区域尺寸 拉伸）。
    # 游戏里 Boss 只按 0.5–0.6 倍显示，存 0.6 倍几乎看不出差别，体积和显存约为三分之一。
    ts = cfg.get("texture_scale", 1.0)
    images = {}
    for motion, ls in motions.items():
        for l in ls:
            im = l["image"]
            if ts != 1.0:
                im = im.resize((max(1, round(im.width * ts)), max(1, round(im.height * ts))), Image.LANCZOS)
            images[f"{motion}/{l['name']}"] = im
    pos, pages = pack(images)
    atlas = []
    for p, (pw, ph) in enumerate(pages):
        page = Image.new("RGBA", (pw, ph))
        for key, (pi, x, y) in pos.items():
            if pi == p:
                page.alpha_composite(images[key], (x, y))
        # 无损 WebP：比 PNG 小约三分之一，模组其余贴图也是 WebP
        page_name = f"{name}.webp" if p == 0 else f"{name}_{p + 1}.webp"
        page.save(out / page_name, lossless=True, method=6)
        if atlas:
            atlas.append("")
        atlas += [page_name, f"size:{pw},{ph}", "filter:Linear,Linear"]
        for key, (pi, x, y) in pos.items():
            if pi == p:
                im = images[key]
                atlas += [key, f"bounds:{x},{y},{im.width},{im.height}"]
    (out / f"{name}.atlas").write_text("\n".join(atlas) + "\n")
    pw, ph = pages[0]

    # 槽位与附件
    slots = []
    attachments = {}
    for motion, ls in motions.items():
        assign = cfg["motions"][motion]["assign"]
        for l in ls:
            role = assign[l["name"]]
            bname = "move" if role == "root" else f"{motion}_{role}"
            sname = f"{motion}/{l['name']}"
            slots.append({"name": sname, "bone": bname, **({"attachment": sname} if motion == setup_motion else {})})
            bx, by = world[bname]
            cx, cy = l["center"][0] + shift[0], l["center"][1] + shift[1]
            attachments[sname] = {sname: {"x": round(cx - bx, 2), "y": round(cy - by, 2),
                                          "width": l["image"].width, "height": l["image"].height}}

    # 动画
    anims = {}
    for aname, a in cfg["animations"].items():
        dur = a["duration"]
        n = max(1, round(dur * fps))
        times = [round(i / fps, 4) for i in range(n + 1)]
        bone_tl = {}
        channels = {}
        for target, ch in a.get("keys", {}).items():
            for c, keys in ch.items():
                channels.setdefault(target, {}).setdefault(c, {})["keys"] = keys
        for target, ch in a.get("osc", {}).items():
            for c, spec in ch.items():
                channels.setdefault(target, {}).setdefault(c, {})["osc"] = spec
        for target, ch in channels.items():
            bname = target.replace(".", "_")
            assert bname in world, f"{aname}: 没有骨头 {target}"

            def val(c, t, default=0.0):
                if c not in ch:
                    return default
                v = eval_keys(ch[c]["keys"], t) if "keys" in ch[c] else default
                if "osc" in ch[c]:
                    v += osc(ch[c]["osc"], t, dur)
                return v

            tl = {}
            if "rotate" in ch:
                tl["rotate"] = [{"time": t, "value": round(val("rotate", t), 3)} for t in times]
            if "x" in ch or "y" in ch:
                tl["translate"] = [{"time": t, "x": round(val("x", t), 2), "y": round(val("y", t), 2)} for t in times]
            if "sx" in ch or "sy" in ch:
                tl["scale"] = [{"time": t, "x": round(val("sx", t, 1.0), 4), "y": round(val("sy", t, 1.0), 4)} for t in times]
            bone_tl[bname] = tl

        # 姿势显隐：show = [[时间, 动作, (旧的淡入时长，已不用)], ...]，到点瞬间换成该动作的整组附件。
        # 不做透明度淡入淡出：Spine 不能把一组层合起来再淡化，逐层半透明时会透出被前发、衣服盖住的底层
        # （光头皮）。原版换动作本来也是瞬间切换。所有槽位每段动画都写附件关键帧，混合时直接取新动画的
        # （mixAttachmentThreshold 默认 0），被打断时也不会两套姿势叠在一起。
        show = a["show"]

        def visible(motion, t):
            cur = None
            for t0, mo, *_ in show:
                if t >= t0 - 1e-6:
                    cur = mo
            return motion == cur

        switch_times = sorted({0.0} | {round(t0, 4) for t0, *_ in show})
        flash = a.get("flash")

        def tint(t):
            if not flash:
                return (1.0, 1.0, 1.0)
            t0, t1, rgb = flash
            if t < t0 or t > t1:
                return (1.0, 1.0, 1.0)
            u = math.sin(math.pi * (t - t0) / (t1 - t0))
            return tuple(1 + (c - 1) * u for c in rgb)

        slot_tl = {}
        for motion, ls in motions.items():
            for l in ls:
                sname = f"{motion}/{l['name']}"
                tl = {"attachment": [{"time": t, "name": sname if visible(motion, t) else None} for t in switch_times]}
                if flash:
                    tl["rgb"] = [{"time": t, "color": "%02x%02x%02x" % tuple(round(c * 255) for c in tint(t))} for t in times]
                slot_tl[sname] = tl
        anims[aname] = {"bones": bone_tl, "slots": slot_tl}

    xs = [world[b["name"]][0] for b in bones]
    skel = {
        "skeleton": {"hash": f"layers-{name}", "spine": "4.2.00", "x": round(min(xs) - 400, 1), "y": -100,
                     "width": 1200, "height": 900, "images": "./", "audio": ""},
        "bones": bones,
        "slots": slots,
        "skins": [{"name": "default", "attachments": attachments}],
        "animations": anims,
    }
    (out / f"{name}.spine-json").write_text(json.dumps(skel, ensure_ascii=False, separators=(",", ":")))
    print(f"{name}: bones={len(bones)} slots={len(slots)} pages={[f"{w}x{h}" for w, h in pages]} anims={list(anims)} -> {out}")


if __name__ == "__main__":
    main()
