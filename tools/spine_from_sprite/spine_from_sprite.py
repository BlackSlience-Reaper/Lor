#!/usr/bin/env python3
"""从一张静态怪物贴图生成 Spine 4.2 骨骼与动画（第一档：整图一个加权网格，不切图）。

用法：
    python3 spine_from_sprite.py <config.json> [--out <目录>]

输出（默认在 config 同目录的 out/）：
    <name>.png            贴图（从源图转成 PNG）
    <name>.atlas          图集（含特效页）
    <name>.spine-json     骨骼数据。扩展名必须是 .spine-json，spine-godot 把其他扩展名当二进制 .skel 读
    fx_*.png              从攻击原图抠出的特效（如果配置了）

坐标约定：配置里的点都是源贴图像素 (px, py)，y 向下；骨架坐标 = (px - W/2, H - py)，原点在贴图底边中点，y 向上。
肢体命名约定：名字以 arm / leg 开头、以 L / R 结尾（armL、legR…），左右镜像、手臂与腿的动作方向都按名字判断。
所有动画按 FPS 采样成线性关键帧；通道值是相对初始姿势的偏移（Spine 4.x 的 rotate/translate 就是叠加在 setup 上），scale 是倍数。
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

FPS = 30


# ---------------------------------------------------------------- 工具

def clamp01(x):
    return max(0.0, min(1.0, x))


def smooth(x):
    x = clamp01(x)
    return x * x * (3 - 2 * x)


def seg(t, a, b):
    """t 在 [a, b] 内的 0..1 平滑进度。"""
    return smooth((t - a) / (b - a)) if b > a else float(t >= b)


def wrap(a):
    return (a + 180) % 360 - 180


def side_sign(limb):
    return 1 if limb.endswith("L") else -1


def is_arm(limb):
    return limb.startswith("arm")


# ---------------------------------------------------------------- 骨架

class Rig:
    def __init__(self, cfg, out_dir):
        self.cfg = cfg
        self.out = out_dir
        self.name = cfg.get("name", "monster")
        src = Image.open(cfg["image"]).convert("RGBA")
        self.img = src
        self.W, self.H = src.size
        # atlas_page 指向现成贴图（与图集同目录、尺寸相同）时不另存 PNG，运行时由引擎按图集目录加载那张贴图
        self.page = cfg.get("atlas_page", f"{self.name}.png")
        if "atlas_page" not in cfg:
            src.save(out_dir / self.page)
        self.bones = [{"name": "root"}]
        self.world = {"root": (0.0, 0.0, 0.0)}  # name -> (x, y, 世界角)
        self.segments = {}
        self.limbs = cfg["limbs"]

    def sk(self, p):
        return (p[0] - self.W / 2, self.H - p[1])

    def add_bone(self, name, parent, start, end=None):
        px, py, pa = self.world[parent]
        sx, sy = start
        angle, length = 0.0, 0.0
        if end is not None:
            angle = math.degrees(math.atan2(end[1] - sy, end[0] - sx))
            length = math.hypot(end[0] - sx, end[1] - sy)
        dx, dy = sx - px, sy - py
        r = math.radians(-pa)
        lx, ly = dx * math.cos(r) - dy * math.sin(r), dx * math.sin(r) + dy * math.cos(r)
        bone = {"name": name, "parent": parent, "x": round(lx, 2), "y": round(ly, 2)}
        if abs(angle - pa) > 1e-6:
            bone["rotation"] = round(angle - pa, 3)
        if length:
            bone["length"] = round(length, 2)
        self.bones.append(bone)
        self.world[name] = (sx, sy, angle)

    def build_bones(self):
        body = self.cfg["body"]
        self.add_bone("body", "root", self.sk(body["center"]))
        for limb, joints in self.limbs.items():
            pts = [self.sk(p) for p in joints]
            parent = "body"
            for i in range(len(pts) - 1):
                name = f"{limb}{i + 1}"
                self.add_bone(name, parent, pts[i], pts[i + 1])
                self.segments[name] = (pts[i], pts[i + 1])
                parent = name
        for leg in self.cfg.get("ik_legs", []):
            self.add_bone(f"{leg}_target", "root", self.sk(self.limbs[leg][2]))

    # 网格：按 cell 像素切格，只保留有不透明像素的格子（外扩一格），每个顶点按到各骨段的距离做高斯权重，最多 3 根骨头
    def build_mesh(self):
        W, H = self.W, self.H
        cell = self.cfg.get("cell", 5)
        sigma = self.cfg.get("sigma", 5.0)
        alpha = self.img.getchannel("A")
        cols, rows = math.ceil(W / cell), math.ceil(H / cell)
        occupied = set()
        for cy in range(rows):
            for cx in range(cols):
                box = (cx * cell, cy * cell, min(W, cx * cell + cell), min(H, cy * cell + cell))
                if alpha.crop(box).getextrema()[1] > 8:
                    occupied.add((cx, cy))
        keep = {(cx + dx, cy + dy) for cx, cy in occupied for dx in (-1, 0, 1) for dy in (-1, 0, 1)
                if 0 <= cx + dx < cols and 0 <= cy + dy < rows}
        vindex, verts, triangles = {}, [], []

        def vid(gx, gy):
            if (gx, gy) not in vindex:
                vindex[(gx, gy)] = len(verts)
                verts.append((min(gx * cell, W), min(gy * cell, H)))
            return vindex[(gx, gy)]

        for cx, cy in sorted(keep):
            a, b, c, d = vid(cx, cy), vid(cx + 1, cy), vid(cx + 1, cy + 1), vid(cx, cy + 1)
            triangles += [a, b, c, a, c, d]

        body = self.cfg["body"]
        bc = self.sk(body["center"])
        rx, ry = body["radii"]

        def body_dist(p):
            n = math.hypot((p[0] - bc[0]) / rx, (p[1] - bc[1]) / ry)
            return max(0.0, (n - 1.0) * min(rx, ry))

        def seg_dist(p, a, b):
            vx, vy = b[0] - a[0], b[1] - a[1]
            t = max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / (vx * vx + vy * vy)))
            return math.hypot(p[0] - a[0] - t * vx, p[1] - a[1] - t * vy)

        index = {b["name"]: i for i, b in enumerate(self.bones)}
        vertex_data, uvs = [], []
        for px, py in verts:
            p = self.sk((px, py))
            dists = {"body": body_dist(p)}
            for name, (a, b) in self.segments.items():
                dists[name] = seg_dist(p, a, b)
            dmin = min(dists.values())
            weights = {n: math.exp(-((d - dmin) ** 2) / (2 * sigma * sigma))
                       for n, d in dists.items() if d - dmin < 3 * sigma}
            top = sorted(weights.items(), key=lambda kv: -kv[1])[:3]
            total = sum(w for _, w in top)
            top = [(n, w / total) for n, w in top if w / total > 0.02]
            total = sum(w for _, w in top)
            vertex_data.append(len(top))
            for n, w in top:
                bx, by, ba = self.world[n]
                dx, dy = p[0] - bx, p[1] - by
                r = math.radians(-ba)
                vertex_data += [index[n], round(dx * math.cos(r) - dy * math.sin(r), 2),
                                round(dx * math.sin(r) + dy * math.cos(r), 2), round(w / total, 4)]
            uvs += [round(px / W, 5), round(py / H, 5)]
        self.mesh = {"type": "mesh", "uvs": uvs, "triangles": triangles, "vertices": vertex_data,
                     "hull": 0, "width": W, "height": H}
        self.vertex_count = len(verts)

    def ik(self):
        out = []
        for leg in self.cfg.get("ik_legs", []):
            (h, k), (_, a) = self.segments[f"{leg}1"], self.segments[f"{leg}2"]
            cross = (k[0] - h[0]) * (a[1] - k[1]) - (k[1] - h[1]) * (a[0] - k[0])
            out.append({"name": f"{leg}_ik", "bones": [f"{leg}1", f"{leg}2"], "target": f"{leg}_target",
                        "bendPositive": cross > 0})
        return out


# ---------------------------------------------------------------- 动画

class Animator:
    def __init__(self, rig, data):
        self.rig = rig
        self.data = data
        self.bones = {b["name"]: b for b in data["bones"]}
        self.world_angle = {}
        for b in data["bones"]:
            parent = b.get("parent")
            self.world_angle[b["name"]] = (self.world_angle[parent] if parent else 0.0) + b.get("rotation", 0.0)
        self.bend = {c["name"]: c.get("bendPositive", True) for c in data.get("ik", [])}
        self.limbs = list(rig.limbs)
        self.ik_names = [c["name"] for c in data.get("ik", [])]

    def segs(self, limb):
        return [n for n in (f"{limb}{i}" for i in range(1, 10)) if n in self.bones]

    def build(self, fn, duration, color=None):
        steps = round(duration * FPS)
        timelines, ik = {}, {}
        for i in range(steps + 1):
            t = duration * i / steps
            ch, mix = fn(t)
            for bone, v in ch.items():
                tl = timelines.setdefault(bone, {})
                if "rotate" in v:
                    tl.setdefault("rotate", []).append({"time": round(t, 4), "value": round(v["rotate"], 3)})
                if "x" in v or "y" in v:
                    tl.setdefault("translate", []).append(
                        {"time": round(t, 4), "x": round(v.get("x", 0.0), 3), "y": round(v.get("y", 0.0), 3)})
                if "sx" in v or "sy" in v:
                    tl.setdefault("scale", []).append(
                        {"time": round(t, 4), "x": round(v.get("sx", 1.0), 4), "y": round(v.get("sy", 1.0), 4)})
            for name, value in mix.items():
                # IK 关键帧缺省 bendPositive=true，必须带上约束自己的弯向，否则会把膝盖翻过来
                ik.setdefault(name, []).append({"time": round(t, 4), "mix": round(value, 4), "bendPositive": self.bend[name]})
        anim = {"bones": timelines}
        if color:
            keys = []
            for i in range(steps + 1):
                t = duration * i / steps
                keys.append({"time": round(t, 4), "color": "".join(f"{round(c * 255):02x}" for c in color(t))})
            anim["slots"] = {self.rig.name: {"rgba": keys}}
        if ik:
            anim["ik"] = ik
        return anim

    # 待机：身体上下浮动并轻微伸缩；手臂各节按延迟正弦摆动（越靠外幅度越大），左右镜像；腿只晃末节
    def idle(self, p):
        T = p.get("duration", 2.4)
        steps = 24
        bob, breath = p.get("bob", 3.5), p.get("breath", 0.015)
        arm_amp = p.get("arm_sway", [3.0, 5.0, 7.0])
        arm_lag = p.get("arm_lag", [0.6, 1.2, 1.8])
        leg_tip = p.get("leg_tip_sway", 2.5)

        def keys(fn):
            return [dict({"time": round(T * i / steps, 4)}, **fn(2 * math.pi * i / steps)) for i in range(steps + 1)]

        def rot(amp, lag):
            return keys(lambda ph: {"value": round(amp * math.sin(ph - lag), 3)})

        bones = {"body": {
            "translate": keys(lambda ph: {"x": 0, "y": round(bob * math.sin(ph), 3)}),
            "scale": keys(lambda ph: {"x": round(1 - breath * math.sin(ph), 4), "y": round(1 + breath * math.sin(ph), 4)}),
        }}
        for side in ("L", "R"):
            sign = 1 if side == "L" else -1
            for limb in self.limbs:
                if not limb.endswith(side):
                    continue
                names = self.segs(limb)
                if is_arm(limb):
                    for i, name in enumerate(names):
                        bones[name] = {"rotate": rot(arm_amp[min(i, len(arm_amp) - 1)] * sign, arm_lag[min(i, len(arm_lag) - 1)])}
                else:
                    bones[names[-1]] = {"rotate": rot(leg_tip * sign, 1.4)}
        return {"bones": bones}

    def straight_offsets(self, limb, radial):
        names = self.segs(limb)
        offs = [wrap(radial - self.world_angle[names[0]])]
        offs += [wrap(-self.bones[n].get("rotation", 0.0)) for n in names[1:]]
        return offs

    # 旋转冲刺：蓄力下蹲后仰 → 四肢拉直成放射状 → 离地转 spins 圈并冲出 dash → 转回落地；旋转期间松开腿 IK
    # 默认时间轴照原版怪物攻击的节奏：命中（冲刺最远）在 dash_out 结束时，约 0.3 秒，对上原版攻击默认等待 0.3 秒
    # （快速模式 0.15 秒）；命中后再转圈、收回。原版快速模式不改动画速度，只缩短等待，所以命中帧要靠前。
    SPIN_DASH_TIMING = {
        "wind": [0.0, 0.1], "extend": [0.08, 0.2], "retract": [0.8, 0.98],
        "spin": [0.12, 0.87], "dash_out": [0.12, 0.3], "dash_back": [0.55, 0.85],
        "lift_up": [0.1, 0.22], "lift_down": [0.82, 0.98], "land": [0.95, 1.1],
        "ik_off": [0.08, 0.14], "ik_on": [0.85, 0.98],
    }

    # 旋转冲刺：蓄力下蹲后仰 → 四肢拉直指向 radial → 离地转 spins 圈并冲出 dash → 转回落地；旋转期间松开腿 IK。
    # 各段起止时间见 SPIN_DASH_TIMING，可在配置的 timing 里逐项覆盖。
    def spin_dash(self, p):
        T = p.get("duration", 1.1)
        tm = dict(self.SPIN_DASH_TIMING, **p.get("timing", {}))
        radial = p.get("radial", {"armL": 135.0, "armR": 45.0, "legL": -135.0, "legR": -45.0})
        straight = {limb: self.straight_offsets(limb, radial[limb]) for limb in self.limbs if limb in radial}
        spins, dash_x, lift_y = p.get("spins", 2), p.get("dash", -150.0), p.get("lift", 34.0)

        def fn(t):
            ch = {}
            # 蓄力姿势只在蓄力段：到四肢拉直时完全退掉，收尾不再回到下蹲
            wind = seg(t, *tm["wind"]) * (1 - seg(t, *tm["extend"]))
            extend = seg(t, *tm["extend"]) * (1 - seg(t, *tm["retract"]))
            spin = 360.0 * spins * smooth((t - tm["spin"][0]) / (tm["spin"][1] - tm["spin"][0]))
            dash = seg(t, *tm["dash_out"]) * (1 - seg(t, *tm["dash_back"]))
            lift = seg(t, *tm["lift_up"]) * (1 - seg(t, *tm["lift_down"]))
            land = math.sin(math.pi * clamp01((t - tm["land"][0]) / (tm["land"][1] - tm["land"][0])))
            ch["body"] = {
                "rotate": -8 * wind + spin,
                "x": 10 * wind + dash_x * dash,
                "y": -8 * wind + lift_y * lift - 5 * land,
                "sx": 1 + 0.05 * wind, "sy": 1 - 0.06 * wind - 0.04 * land,
            }
            for limb, offs in straight.items():
                sign = side_sign(limb)
                names = self.segs(limb)
                for i, name in enumerate(names):
                    v = offs[i] * extend
                    if is_arm(limb) and i == 0:
                        v += 12 * sign * wind
                    if is_arm(limb) and i == 1:
                        v += -10 * sign * wind
                    ch[name] = {"rotate": v}
            ik = 1 - seg(t, *tm["ik_off"]) * (1 - seg(t, *tm["ik_on"]))
            return ch, {n: ik for n in self.ik_names}

        anim = self.build(fn, T)
        if "fx" in p:
            fx = dict({"on": tm["spin"][0], "off": tm["spin"][1] + 0.03}, **p["fx"])
            self.add_spin_fx(anim, fx, T)
        return anim

    def add_spin_fx(self, anim, fx, T):
        """旋转期间显示的特效图层：挂在 body 下的 fx 骨上，额外自转并放大，淡入淡出。"""
        on, off = fx.get("on", 0.34), fx.get("off", 1.0)
        extra = fx.get("extra_spin", 400.0)
        steps = round(T * FPS)
        rot, scale, rgba = [], [], []
        for i in range(steps + 1):
            t = T * i / steps
            a = seg(t, on, on + 0.1) * (1 - seg(t, off - 0.16, off))
            rot.append({"time": round(t, 4), "value": round(extra * smooth((t - on) / (off - on)), 3)})
            s = fx.get("scale_from", 0.82) + (fx.get("scale_to", 1.02) - fx.get("scale_from", 0.82)) * seg(t, on, on + 0.16)
            scale.append({"time": round(t, 4), "x": round(s, 4), "y": round(s, 4)})
            rgba.append({"time": round(t, 4), "color": f"ffffff{round(a * 255):02x}"})
        anim["bones"]["fx"] = {"rotate": rot, "scale": scale}
        anim.setdefault("slots", {})["fx"] = {
            "attachment": [{"time": round(on, 4), "name": "fx"}, {"time": off, "name": None}],
            "rgba": rgba,
        }

    # 受击：向后（+x）击退、后仰、压扁，四肢带衰减乱甩，脚踩地；可选闪红
    def hurt(self, p):
        T = p.get("duration", 0.6)
        kb, tilt = p.get("knockback", 30.0), p.get("tilt", -20.0)
        flail = p.get("flail", [24.0, 34.0, 42.0])

        def fn(t):
            hit = seg(t, 0.0, 0.05) * (1 - seg(t, 0.1, T))
            wob = math.exp(-6 * t) * math.sin(24 * t)
            ch = {"body": {"rotate": tilt * hit, "x": kb * hit, "y": 6 * hit,
                           "sx": 1 - 0.08 * hit, "sy": 1 + 0.09 * hit}}
            for limb in self.limbs:
                sign, up = side_sign(limb), (1 if is_arm(limb) else -1)
                names = self.segs(limb)
                ch[names[0]] = {"rotate": sign * up * flail[0] * hit + 10 * wob}
                if len(names) > 1:
                    ch[names[1]] = {"rotate": -sign * flail[1] * wob}
                for n in names[2:]:
                    ch[n] = {"rotate": sign * flail[2] * wob}
            return ch, {n: 1.0 for n in self.ik_names}

        flash = p.get("flash", 0.45)

        def color(t):
            k = seg(t, 0.0, 0.05) * (1 - seg(t, 0.08, 0.3))
            return (1.0, 1 - flash * k, 1 - flash * k, 1.0)

        return self.build(fn, T, color if flash else None)

    # 死亡：抖一下 → 松开腿 IK、带重力摔到 rest_height → 弹一下 → 停在末姿势。末姿势按各节“目标世界角”反推
    def die(self, p):
        T = p.get("duration", 1.6)
        body_rot, body_x = p.get("body_rotate", -14.0), p.get("body_x", 22.0)
        body_y = p.get("rest_height", 52.0) - self.bones["body"]["y"]
        targets = p["world_angles"]
        offs = {}
        for limb, angles in targets.items():
            acc, out = 0.0, []
            for name, target in zip(self.segs(limb), angles):
                off = wrap(target - self.world_angle[name] - body_rot - acc)
                out.append(off)
                acc += off
            offs[limb] = out

        def fn(t):
            jolt = seg(t, 0.0, 0.05) * (1 - seg(t, 0.1, 0.25))
            fall = clamp01((t - 0.15) / 0.55) ** 2
            land = clamp01((t - 0.7) / 0.3)
            bounce = math.sin(math.pi * land) * (1 - land) * (t >= 0.7)
            limp = seg(t, 0.2, 1.0)
            fl = math.sin(math.pi * clamp01((t - 0.15) / 0.55))
            ch = {"body": {"rotate": -10 * jolt + body_rot * limp, "x": 10 * jolt + body_x * limp,
                           "y": body_y * fall + 10 * bounce, "sx": 1 + 0.08 * bounce, "sy": 1 - 0.1 * bounce}}
            for limb, o in offs.items():
                sign, up = side_sign(limb), (1 if is_arm(limb) else -1)
                names = self.segs(limb)
                for i, name in enumerate(names):
                    v = o[i] * limp
                    if i == 0:
                        v += sign * up * 12 * fl
                    elif i == 1:
                        v -= sign * 10 * fl
                    ch[name] = {"rotate": v}
            ik = 1 - seg(t, 0.15, 0.35)
            return ch, {n: ik for n in self.ik_names}

        return self.build(fn, T)


# ---------------------------------------------------------------- 特效抠图

def extract_fx(fx, out_dir, page):
    """按颜色从原图抠特效（默认“红色占优”），挖掉 pivot 半径 inner 以内，返回 (图集尺寸, 相对 body 的偏移)。"""
    src = Image.open(fx["source"]).convert("RGBA")
    a = np.asarray(src).astype(np.float32)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    lo, span = fx.get("key_low", 25.0), fx.get("key_span", 70.0)
    k = np.clip(((r - np.maximum(g, b)) - lo) / span, 0, 1) * (al / 255)
    out = np.zeros_like(a)
    out[..., :3] = a[..., :3]
    out[..., 3] = 255 * k
    img = Image.fromarray(out.astype(np.uint8), "RGBA")
    box = img.getchannel("A").point(lambda v: 255 if v > 10 else 0).getbbox()
    crop = np.asarray(img.crop(box)).copy()
    px, py = fx["pivot"][0] - box[0], fx["pivot"][1] - box[1]
    yy, xx = np.mgrid[0:crop.shape[0], 0:crop.shape[1]]
    dist = np.hypot(xx - px, yy - py)
    inner = fx.get("inner_radius", 140.0)
    crop[..., 3] = (crop[..., 3] * np.clip((dist - inner) / 20, 0, 1)).astype(np.uint8)
    fx_img = Image.fromarray(crop, "RGBA")
    fx_img.save(out_dir / page)
    w, h = fx_img.size
    return (w, h), (w / 2 - px, py - h / 2)


# ---------------------------------------------------------------- 主流程

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("config")
    ap.add_argument("--out")
    args = ap.parse_args()
    cfg_path = Path(args.config).resolve()
    cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
    out_dir = Path(args.out) if args.out else cfg_path.parent / "out"
    out_dir.mkdir(parents=True, exist_ok=True)
    # 配置里的贴图路径可以写相对路径，按配置文件所在目录解析
    cfg["image"] = str((cfg_path.parent / cfg["image"]).resolve())
    for anim in cfg.get("animations", {}).values():
        if "fx" in anim:
            anim["fx"]["source"] = str((cfg_path.parent / anim["fx"]["source"]).resolve())

    rig = Rig(cfg, out_dir)
    rig.build_bones()
    rig.build_mesh()
    name, W, H = rig.name, rig.W, rig.H
    data = {
        "skeleton": {"hash": f"tier1-{name}", "spine": "4.2.00", "x": -W / 2, "y": 0, "width": W, "height": H,
                     "images": "./", "audio": ""},
        "bones": rig.bones,
        "slots": [{"name": name, "bone": "root", "attachment": name}],
        "ik": rig.ik(),
        "skins": [{"name": "default", "attachments": {name: {name: rig.mesh}}}],
        "animations": {},
    }
    atlas = f"{rig.page}\nsize:{W},{H}\nfilter:Linear,Linear\n{name}\nbounds:0,0,{W},{H}\n"

    anims = cfg.get("animations", {})
    fx_cfg = next((a["fx"] for a in anims.values() if "fx" in a), None)
    if fx_cfg:
        fx_page = fx_cfg.get("page", "fx.png")
        (fw, fh), (ox, oy) = extract_fx(fx_cfg, out_dir, fx_page)
        data["bones"].append({"name": "fx", "parent": "body"})
        data["slots"].insert(0, {"name": "fx", "bone": "fx"})  # 排在本体后面，初始不显示
        data["skins"][0]["attachments"]["fx"] = {"fx": {"x": ox, "y": oy, "width": fw, "height": fh}}
        atlas += f"\n{fx_page}\nsize:{fw},{fh}\nfilter:Linear,Linear\nfx\nbounds:0,0,{fw},{fh}\n"

    animator = Animator(rig, data)
    for anim_name, p in anims.items():
        kind = p.get("type", anim_name)
        data["animations"][anim_name] = getattr(animator, kind)(p)

    (out_dir / f"{name}.spine-json").write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
    (out_dir / f"{name}.atlas").write_text(atlas, encoding="utf-8")
    print(f"{name}: bones={len(data['bones'])} verts={rig.vertex_count} tris={len(rig.mesh['triangles']) // 3} "
          f"ik={[c['bendPositive'] for c in data['ik']]} anims={list(data['animations'])} -> {out_dir}")


if __name__ == "__main__":
    main()
