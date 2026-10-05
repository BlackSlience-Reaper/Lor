"""原版只有整图的怪物：把模组的逐帧贴图做成和 export_layers.py 同格式的分层目录，再走 build_boss_configs 的骨架流程。
每张贴图一个动作（一层 body），按模组换图外观的摆放规则放到同一坐标系里，换姿势时位置与原来的逐帧换图一致；
硬的小部件（大鸟的提灯）可以按椭圆加亮度阈值抠成单独的层，挂自己的骨头摆动。

坐标：以外观的贴图 Position 为原点，y 向上，单位是待机贴图像素。摆放规则同 SpriteAttackCreatureVisuals：
- visible_bottom（默认锚点）：贴图可见底边（alpha > 0.03 的最低行）的中点落在 Position + Nudge；
- center（Profile.Centered()）：贴图中心落在 Position + Nudge。
Nudge 是父节点单位，除以贴图缩放换成待机像素；Lunge 招式不用 Nudge。帧另给 Scale 时按 帧缩放/待机缩放 缩放贴图。
骨架原点要放在该形态待机贴图的矩形底边中点（RuntimeSpineBody.AlignTo），打印在 origins 里，填进 build_boss_configs。
用法：sprite_layers.py [名字...]"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

M = Path(__file__).resolve().parents[2] / "images/monsters"
LAYERS = Path.home() / ".local/share/LibraryOfRuina-layers"

SPRITES = {
    # 外观 ForgottenKnightSwordCreatureVisuals：三个形态，攻击帧 Nudge(-46, -10)。
    # 打击、斩击、突刺三张图里的剑画得只有待机的约一半（打击、斩击剑柄头到剑尖约 190 像素，待机约 350；突刺按剑柄比例约 0.5），
    # 换姿势时剑忽大忽小，fix 放大回去；招架、受击与待机同大。绝望形态整套比另两个形态大约 1.27 倍，形态内一致，没动
    "forgotten_sword": {
        "scale": 0.54, "anchor": "visible_bottom",
        "idles": {"Normal": "forgotten_knight_sword/normal_idle.png", "Teardrop": "forgotten_knight_sword/teardrop_idle.png",
                  "Despair": "forgotten_knight_sword/despair_idle.png"},
        "poses": {
            "NormalBlunt": {"image": "forgotten_knight_sword/normal_blunt.png", "nudge": [-46, -10], "fix": 1.85},
            "NormalPierce": {"image": "forgotten_knight_sword/normal_pierce.png", "nudge": [-46, -10], "fix": 2.0},
            "NormalSlash": {"image": "forgotten_knight_sword/normal_slash.png", "nudge": [-46, -10], "fix": 1.85},
            "NormalParry": {"image": "forgotten_knight_sword/normal_parry.png"},
            "NormalHit": {"image": "forgotten_knight_sword/normal_hit.png"},
            "TeardropBlunt": {"image": "forgotten_knight_sword/teardrop_blunt.png", "nudge": [-46, -10], "fix": 1.85},
            "TeardropPierce": {"image": "forgotten_knight_sword/teardrop_pierce.png", "nudge": [-46, -10], "fix": 2.0},
            "TeardropSlash": {"image": "forgotten_knight_sword/teardrop_slash.png", "nudge": [-46, -10], "fix": 1.85},
            "TeardropParry": {"image": "forgotten_knight_sword/teardrop_parry.png"},
            "TeardropHit": {"image": "forgotten_knight_sword/teardrop_hit.png"},
            "DespairAttack": {"image": "forgotten_knight_sword/despair_attack.png", "nudge": [-46, -10]},
            "DespairHit": {"image": "forgotten_knight_sword/despair_hit.png"},
        },
    },
    # PriceOfSilenceCreatureVisuals：特殊帧 Nudge(-18, 10)
    "price_of_silence": {
        "scale": 0.56, "anchor": "visible_bottom",
        "idles": {"Default": "price_of_silence/idle.png"},
        "poses": {"Special": {"image": "price_of_silence/special.png", "nudge": [-18, 10]}},
    },
    # BigBirdCreatureVisuals：普通、沉睡、救赎（张开）三个形态；普通待机的提灯抠出来摆动（暗色的爪子留在身体上）
    "big_bird": {
        # 救赎形态的红提灯垂得比爪子低，按可见底边对齐时爪子会悬空；用户要求爪子始终同一高度，pin_y 按爪尖对齐
        "scale": 0.60, "anchor": "visible_bottom", "pin_y": "dark_bottom",
        # 防御、魅惑、救赎张开三张图里的鸟画得比待机小（按黑色身体的面积和高度量，约 1.33、1.45、1.15 倍），
        # 原来逐帧换图时会忽大忽小；fix 把鸟放大回待机的大小
        "idles": {"Default": "big_bird/idle.png", "Sleep": "big_bird/sleep.png",
                  "RescueOpen": {"image": "big_bird/rescue_open.png", "fix": 1.15}},
        "poses": {"Hit": {"image": "big_bird/hit.png"}, "Guard": {"image": "big_bird/guard.png", "fix": 1.33},
                  "Charm": {"image": "big_bird/charm.png", "fix": 1.45},
                  "RescueClose": {"image": "big_bird/rescue_close.png"}},
        # 光晕盖在爪子上、爪子又挡着光晕，抠出来会带爪形缺口：身体上去掉椭圆里所有亮像素（爪子是暗的，留下），
        # 提灯层是提灯本体（box）加一圈按原图取色重画的径向光晕，跟着提灯一起摆
        "cutouts": {"Default": [{"name": "lantern", "ellipse": [100, 405, 108, 108], "min_lum": 110,
                                 "box": [78, 364, 126, 440],
                                 "glow": {"center": [100, 405], "radius": 98, "core": 0.42, "alpha": 250,
                                          "inner": [255, 246, 206], "outer": [255, 224, 70]}}]},
    },
    # ForsakenMurdererCreatureVisuals：Centered，攻击是 Lunge（不用 Nudge）、Scale(0.33)。
    # 跪着的人：按图片中心对齐时换姿势膝盖会上下跳，用户要求膝盖始终在同一高度——pin 是各图里膝盖着地的点
    # （束缚衣最下面一段的左端），各姿势按它对齐到待机图的膝盖
    "forsaken_murderer": {
        "scale": 0.31, "anchor": "center",
        "idles": {"Default": {"image": "forsaken_murderer.webp", "pin": [320, 407]}},
        "poses": {"Attack": {"image": "forsaken_murderer_attack.webp", "frame_scale": 0.33, "pin": [750, 652]},
                  "Hit": {"image": "forsaken_murderer_hit.webp", "pin": [30, 693]}},
    },
}


def visible_bottom(im):
    a = np.asarray(im.getchannel("A"))
    rows = np.nonzero((a > 0.03 * 255).any(axis=1))[0]
    return float(rows[-1] + 1) if len(rows) else float(im.height)


def build(name, spec):
    out = LAYERS / name
    out.mkdir(parents=True, exist_ok=True)
    s = spec["scale"]
    info = {}
    origins = {}
    pin_ref = None
    pin_y_ref = None
    poses = {**{k: {**(v if isinstance(v, dict) else {"image": v}), "idle": True} for k, v in spec["idles"].items()},
             **spec["poses"]}
    for motion, p in poses.items():
        im = Image.open(M / p["image"]).convert("RGBA")
        k0 = p.get("frame_scale", s) / s
        k = k0 * p.get("fix", 1.0)
        if p.get("idle"):
            # 骨架原点对齐的是游戏里没放大的那张待机贴图（RuntimeSpineBody.AlignTo），原点按它算
            raw = Image.open(M / p["image"]).convert("RGBA")
            if k0 != 1.0:
                raw = raw.resize((round(raw.width * k0), round(raw.height * k0)), Image.LANCZOS)
            rH = raw.height
            origins[motion] = ([0.0, round(-(rH - visible_bottom(raw)), 1)] if spec["anchor"] == "visible_bottom"
                               else [0.0, round(-rH / 2, 1)])
        if k != 1.0:
            im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
        nx, ny = p.get("nudge", [0, 0])
        W, H = im.size
        if spec["anchor"] == "visible_bottom":
            vb = visible_bottom(im)
            cx, cy = nx / s, -ny / s + (vb - H / 2)
        else:
            cx, cy = nx / s, -ny / s
        if spec.get("pin_y") == "dark_bottom":
            # 只对高度：各姿势最低的暗色像素（大鸟的爪尖）落在同一高度。可见底边可能是低垂的提灯光晕，不能用它对
            a = np.asarray(im).astype(np.float32)
            rows = np.nonzero(((a[..., 3] > 200) & (a[..., :3].max(-1) < 70)).any(axis=1))[0]
            py = rows[-1] + 1 - H / 2
            if pin_y_ref is None:
                pin_y_ref = cy - py
            else:
                cy = pin_y_ref + py
        if "pin" in p:
            # pin（原图像素）：第一个带 pin 的待机图定下参照点，其余带 pin 的姿势把自己的 pin 对到这一点
            px, py = p["pin"][0] * k - W / 2, p["pin"][1] * k - H / 2
            if pin_ref is None:
                pin_ref = (cx + px, cy - py)
            else:
                cx, cy = pin_ref[0] - px, pin_ref[1] + py
        (out / motion).mkdir(exist_ok=True)
        layers = []
        body = im.copy()
        for i, c in enumerate(spec.get("cutouts", {}).get(motion, [])):
            ex, ey, rx, ry = c["ellipse"]
            arr = np.asarray(im).astype(np.float32)
            yy, xx = np.mgrid[0:H, 0:W]
            inside = ((xx - ex) / rx) ** 2 + ((yy - ey) / ry) ** 2 <= 1
            lum = arr[..., :3].max(-1)
            # 椭圆里够亮的像素从身体上去掉；box（部件本体，含暗色框线）整块去掉
            take = inside & (lum >= c["min_lum"]) & (arr[..., 3] > 0)
            box = np.zeros_like(take)
            if "box" in c:
                bx0, by0, bx1, by1 = c["box"]
                # box 里只取部件本身（有色或暗的像素），不取淡色光晕，否则光晕重画后会叠出一块方形
                sat = lum - arr[..., :3].min(-1)
                box = ((xx >= bx0) & (xx <= bx1) & (yy >= by0) & (yy <= by1) & (arr[..., 3] > 0)
                       & ((sat >= 80) | (lum < 200)))
            part = arr.copy()
            part[..., 3] = np.where(take | box, arr[..., 3], 0)
            rest = arr.copy()
            rest[..., 3] = np.where(take | box, 0, arr[..., 3])
            body = Image.fromarray(rest.astype(np.uint8), "RGBA")
            part_im = Image.fromarray(part.astype(np.uint8), "RGBA")
            if "glow" in c:
                g = c["glow"]
                t = np.hypot(xx - g["center"][0], yy - g["center"][1]) / g["radius"]
                a = g["alpha"] * np.clip((1 - t) / (1 - g["core"]), 0, 1) ** 1.6
                mix = np.clip(t, 0, 1)[..., None]
                rgb = np.array(g["inner"]) * (1 - mix) + np.array(g["outer"]) * mix
                glow = Image.fromarray(np.dstack([rgb, a]).astype(np.uint8), "RGBA")
                only_box = part.copy()
                only_box[..., 3] = np.where(box, arr[..., 3], 0)
                glow.alpha_composite(Image.fromarray(only_box.astype(np.uint8), "RGBA"))
                part_im = glow
            part_im.save(out / motion / f"{c['name']}.png")
            layers.append({"name": c["name"], "type": 9, "order": 10 + i, "center": [cx, cy]})
        body.save(out / motion / "body.png")
        layers.insert(0, {"name": "body", "type": 2, "order": 1, "center": [cx, cy]})
        for l in layers:
            l.update({"enabled": True, "active": True, "pos": l["center"], "rot": 0.0, "scale": [1.0, 1.0],
                      "size": [W, H], "pivot": [0.5, 0.5], "ppu": 100.0, "motion": motion})
            l["center"] = [round(v, 2) for v in l["center"]]
        info[motion] = {"layers": layers, "composite_origin": [round(W / 2 - cx, 2), round(cy + H / 2, 2)]}
        im.save(out / f"{motion}.composite.png")
    (out / "layers.json").write_text(json.dumps(info, ensure_ascii=False, indent=1))
    print(name, "origins", json.dumps(origins))


def main():
    only = set(sys.argv[1:])
    for name, spec in SPRITES.items():
        if not only or name in only:
            build(name, spec)


if __name__ == "__main__":
    main()
