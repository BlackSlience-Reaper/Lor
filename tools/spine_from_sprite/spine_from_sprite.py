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
        if "atlas_page" not in cfg and "layers" not in cfg:
            src.save(out_dir / self.page)
        self.bones = [{"name": "root"}]
        self.world = {"root": (0.0, 0.0, 0.0)}  # name -> (x, y, 世界角)
        self.segments = {}
        self.limbs = cfg.get("limbs", {})

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
        # 椭圆部件（头、群体里的每一只……）：骨头放在 bone 点（例如翅根，扇翅时以它为轴），蒙皮区域是 center/radii 椭圆
        self.parts = self.cfg.get("parts", {})
        for name, part in self.parts.items():
            self.add_bone(name, part.get("parent", "body"), self.sk(part.get("bone", part["center"])))
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
    # 分层时每层单独建网格：alpha 是该层贴图的透明度，allowed 是该层能绑的骨头（只按这些骨头算权重）
    def build_mesh(self, alpha=None, allowed=None):
        W, H = self.W, self.H
        cell = self.cfg.get("cell", 5)
        sigma = self.cfg.get("sigma", 5.0)
        if alpha is None:
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

        def ellipse_dist(center, radii):
            c = self.sk(center)
            rx, ry = radii

            def dist(p):
                n = math.hypot((p[0] - c[0]) / rx, (p[1] - c[1]) / ry)
                return max(0.0, (n - 1.0) * min(rx, ry))

            return dist

        body = self.cfg["body"]
        # skin=false：身体只当分组中心（例如蝴蝶群），不参与蒙皮
        regions = {}
        if body.get("skin", True):
            regions["body"] = ellipse_dist(body["center"], body["radii"])
        for name, part in self.parts.items():
            regions[name] = ellipse_dist(part["center"], part["radii"])

        def seg_dist(p, a, b):
            vx, vy = b[0] - a[0], b[1] - a[1]
            t = max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / (vx * vx + vy * vy)))
            return math.hypot(p[0] - a[0] - t * vx, p[1] - a[1] - t * vy)

        segments = self.segments
        if allowed is not None:
            regions = {n: d for n, d in regions.items() if n in allowed}
            segments = {n: s for n, s in segments.items() if n in allowed}
            if not regions and not segments:
                # 该层只跟一根没有蒙皮区域的骨头走（例如躯干层跟 body）：所有顶点全权重给它
                regions = {n: (lambda p: 0.0) for n in allowed}

        index = {b["name"]: i for i, b in enumerate(self.bones)}
        vertex_data, uvs = [], []
        dominant = []
        for px, py in verts:
            p = self.sk((px, py))
            dists = {name: dist(p) for name, dist in regions.items()}
            for name, (a, b) in segments.items():
                dists[name] = seg_dist(p, a, b)
            dmin = min(dists.values())
            weights = {n: math.exp(-((d - dmin) ** 2) / (2 * sigma * sigma))
                       for n, d in dists.items() if d - dmin < 3 * sigma}
            top = sorted(weights.items(), key=lambda kv: -kv[1])[:3]
            total = sum(w for _, w in top)
            top = [(n, w / total) for n, w in top if w / total > 0.02]
            total = sum(w for _, w in top)
            vertex_data.append(len(top))
            dominant.append(top[0][0])
            for n, w in top:
                bx, by, ba = self.world[n]
                dx, dy = p[0] - bx, p[1] - by
                r = math.radians(-ba)
                vertex_data += [index[n], round(dx * math.cos(r) - dy * math.sin(r), 2),
                                round(dx * math.sin(r) + dy * math.cos(r), 2), round(w / total, 4)]
            uvs += [round(px / W, 5), round(py / H, 5)]
        # 同一网格内按三角形顺序绘制、后画的在上。网格按列从左往右排，挥到身体前面的武器会被身体盖住；
        # draw_on_top 列出的骨头为主的三角形挪到最后画。
        on_top = set(self.cfg.get("draw_on_top", []))
        if on_top:
            tris = [triangles[i:i + 3] for i in range(0, len(triangles), 3)]
            front = [t for t in tris if any(dominant[v] in on_top for v in t)]
            back = [t for t in tris if not any(dominant[v] in on_top for v in t)]
            triangles = [v for t in back + front for v in t]
        mesh = {"type": "mesh", "uvs": uvs, "triangles": triangles, "vertices": vertex_data,
                "hull": 0, "width": W, "height": H}
        if allowed is not None:
            return mesh, len(verts)
        self.mesh = mesh
        self.vertex_count = len(verts)

    # 分层：layers 按从后到前的顺序列出图层，除第一层（底层，通常是躯干）外每层用 mask 圈出自己的像素；
    # 底层是剩下的像素，再在 inpaint 列出的区域里按周围纹理补齐（被手臂、头挡住的部分，手臂挥开时不会露洞）。
    # 每层存成一张页面贴图、单独建网格、只绑 bones 列出的骨头（躯干、头整块跟一根骨头走，不再拉伸）。
    def build_layers(self):
        import cv2

        W, H = self.W, self.H
        src = np.asarray(self.img).copy()
        taken = np.zeros((H, W), np.float32)
        layers = self.cfg["layers"]
        out = []

        def shape_mask(spec):
            m = np.zeros((H, W), np.uint8)
            for poly in spec.get("polygons", []):
                cv2.fillPoly(m, [np.array(poly, np.int32)], 255)
            for cx, cy, rx, ry in spec.get("ellipses", []):
                cv2.ellipse(m, (int(cx), int(cy)), (int(rx), int(ry)), 0, 0, 360, 255, -1)
            for x1, y1, x2, y2, r in spec.get("capsules", []):
                cv2.line(m, (int(x1), int(y1)), (int(x2), int(y2)), 255, int(2 * r))
            return m

        images = {}
        for layer in layers[1:]:
            # 遮罩外扩 grow 像素：原图描边外侧的抗锯齿像素要跟着这一层走，否则底层会留一圈细白边
            grow = int(layer.get("grow", 3))
            m = shape_mask(layer["mask"])
            if grow > 0:
                m = cv2.dilate(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * grow + 1, 2 * grow + 1)))
            m = m.astype(np.float32) / 255
            img = src.copy()
            img[..., 3] = (img[..., 3] * m).astype(np.uint8)
            images[layer["name"]] = img
            taken = np.maximum(taken, m)
        base = src.copy()
        # keep：两层交界的描边线两边都留。上层盖着时看不出重复，上层挪开后底层边缘仍有描边
        keep = shape_mask(layers[0].get("keep", {})).astype(np.float32) / 255
        base[..., 3] = (base[..., 3] * (1 - taken * (1 - keep))).astype(np.uint8)
        fill = shape_mask({"polygons": layers[0].get("inpaint", []), "ellipses": layers[0].get("inpaint_ellipses", [])})
        if fill.any():
            # 只用剩下的躯干像素推算补洞颜色：被别的层拿走的像素、透明像素（RGB 可能是白的）都当作未知，
            # 免得手臂、头（黄眼、红嘴）的颜色渗进来
            unknown = cv2.bitwise_or(fill, ((taken > 0.5) | (src[..., 3] < 128)).astype(np.uint8) * 255)
            rgb = cv2.inpaint(np.ascontiguousarray(src[..., :3]), unknown, 9, cv2.INPAINT_TELEA)
            base[..., :3] = np.where(fill[..., None] > 0, rgb, base[..., :3])
            base[..., 3] = np.maximum(base[..., 3], fill)
        images[layers[0]["name"]] = base

        for layer in layers:
            img = Image.fromarray(images[layer["name"]], "RGBA")
            page = f"{self.name}_{layer['name']}.png"
            img.save(self.out / page)
            mesh, count = self.build_mesh(img.getchannel("A"), set(layer["bones"]))
            out.append((layer["name"], page, mesh, count))
        return out

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
            anim["slots"] = {slot: {"rgba": keys} for slot in self.rig.slot_names}
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
        # sway：身体绕自己的骨头左右摆（度）。骨头放在脚下时就是插在地上的杆子随风晃
        if p.get("sway"):
            bones["body"]["rotate"] = keys(lambda ph: {"value": round(p["sway"] * math.sin(ph + 0.7), 3)})
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

        # 部件：bob 上下、sway 左右、rotate 摆角、flap 扇翅（以骨头为轴横向缩放，flap_cycles 是每个循环扇几下），phase 错开
        parts = p.get("parts", {})
        n = 96

        def pkeys(fn):
            return [dict({"time": round(T * i / n, 4)}, **fn(2 * math.pi * i / n)) for i in range(n + 1)]

        for name, o in parts.items():
            ph0 = o.get("phase", 0.0)
            ch = {}
            if o.get("bob") or o.get("sway"):
                ch["translate"] = pkeys(lambda ph, o=o, ph0=ph0: {
                    "x": round(o.get("sway", 0.0) * math.sin(ph + ph0 + 1.1), 3),
                    "y": round(o.get("bob", 0.0) * math.sin(ph + ph0), 3)})
            if o.get("rotate"):
                ch["rotate"] = pkeys(lambda ph, o=o, ph0=ph0: {"value": round(o["rotate"] * math.sin(ph + ph0 - 0.5), 3)})
            if o.get("flap"):
                ch["scale"] = pkeys(lambda ph, o=o, ph0=ph0: {
                    "x": round(1 - o["flap"] * (0.5 - 0.5 * math.cos(o.get("flap_cycles", 4) * ph + ph0)), 4), "y": 1.0})
            if ch:
                bones[name] = ch
        return {"bones": bones}

    def part_names(self):
        return list(self.rig.parts)

    def part_local(self, name):
        return self.bones[name].get("x", 0.0), self.bones[name].get("y", 0.0)

    # 群体攻击（蝴蝶群等，身体只当分组中心）：各部件往中心聚拢 → 整群冲出 dash（命中在 dash_out 结束，默认 0.3 秒）→ 散回原位；
    # 全程快速扇翅。fx 默认在命中前后炸开。
    def swarm_attack(self, p):
        T = p.get("duration", 1.0)
        tm = dict({"gather": [0.0, 0.16], "dash_out": [0.12, 0.3], "dash_back": [0.5, 0.85], "scatter": [0.32, 0.6]},
                  **p.get("timing", {}))
        dash_x, gather = p.get("dash", -120.0), p.get("gather", 0.55)
        flap, hz = p.get("flap", 0.6), p.get("flap_hz", 7.0)
        parts = self.part_names()

        def fn(t):
            g = seg(t, *tm["gather"]) * (1 - seg(t, *tm["scatter"]))
            dash = seg(t, *tm["dash_out"]) * (1 - seg(t, *tm["dash_back"]))
            ch = {"body": {"x": dash_x * dash, "y": 8 * math.sin(math.pi * clamp01(t / T))}}
            for i, name in enumerate(parts):
                bx, by = self.part_local(name)
                f = 0.5 - 0.5 * math.cos(2 * math.pi * hz * t + i * 1.3)
                ch[name] = {"x": -bx * gather * g, "y": -by * gather * g,
                            "rotate": 14 * dash * (1 if i % 2 else -1), "sx": 1 - flap * f, "sy": 1.0}
            return ch, {}

        anim = self.build(fn, T)
        if "fx" in p:
            fx = dict({"on": 0.24, "off": 0.62, "extra_spin": 0.0, "scale_from": 0.5, "scale_to": 1.1,
                       "fade_in": 0.05, "fade_out": 0.25, "scale_time": 0.12}, **p["fx"])
            self.add_spin_fx(anim, fx, T)
        return anim

    # 群体受击：整群向后（+x）退，各部件被打散（离中心 spread 倍）、乱转、扇翅变快，再飞回；可选闪红
    def swarm_hurt(self, p):
        T = p.get("duration", 0.6)
        kb, spread, flash = p.get("knockback", 24.0), p.get("spread", 0.35), p.get("flash", 0.45)
        parts = self.part_names()

        def fn(t):
            hit = seg(t, 0.0, 0.05) * (1 - seg(t, 0.12, T))
            ch = {"body": {"x": kb * hit}}
            for i, name in enumerate(parts):
                bx, by = self.part_local(name)
                wob = math.exp(-6 * t) * math.sin(22 * t + i)
                f = 0.5 - 0.5 * math.cos(2 * math.pi * 9 * t + i)
                ch[name] = {"x": bx * spread * hit, "y": by * spread * hit,
                            "rotate": 25 * wob * (1 if i % 2 else -1), "sx": 1 - 0.5 * f, "sy": 1.0}
            return ch, {}

        def color(t):
            k = seg(t, 0.0, 0.05) * (1 - seg(t, 0.08, 0.3))
            return (1.0, 1 - flash * k, 1 - flash * k, 1.0)

        return self.build(fn, T, color if flash else None)

    # 群体死亡：各部件按 stagger 先后停止扇翅、打着旋飘落到 rest_heights（骨头离地高度，默认椭圆半高的一半），停在地上
    def swarm_die(self, p):
        T = p.get("duration", 1.8)
        stagger, fall_time = p.get("stagger", 0.18), p.get("fall_time", 0.9)
        rest = p.get("rest_heights", {})
        parts = self.part_names()

        def fn(t):
            ch = {}
            for i, name in enumerate(parts):
                t0 = 0.1 + i * stagger
                fall = clamp01((t - t0) / fall_time)
                ease = fall ** 1.6
                bone_y = self.rig.world[name][1]
                target = rest.get(name, self.rig.parts[name]["radii"][1] * 0.5)
                flutter = math.sin(math.pi * fall)
                beat = (1 - fall) * (0.5 - 0.5 * math.cos(2 * math.pi * 6 * t + i))
                # 落下时各自往外飘开（离分组中心 drift 倍），免得叠成一堆
                # drift_x 可按部件直接指定横向落点偏移（像素）
                bx, _ = self.part_local(name)
                dx = p.get("drift_x", {}).get(name, bx * p.get("drift", 0.6))
                ch[name] = {"x": 16 * flutter * math.sin(5 * t + i) + dx * smooth(fall),
                            "y": (target - bone_y) * ease,
                            "rotate": (40 if i % 2 else -40) * smooth(fall), "sx": 1 - 0.6 * beat - 0.3 * smooth(fall), "sy": 1.0}
            return ch, {}

        return self.build(fn, T)

    # ---- 关节链（分层后用）：每节给目标世界角，按数值插值，转向由数值决定 ----
    # 初始世界角归一到 [0, 360)，配置里的角度从它出发按数值走，例如初始 251° → 115° 是经过 180°（前方）往上抬。
    # 每节的旋转偏移 = 目标世界角 − 初始世界角 − 父链（身体旋转 + 上几节偏移）之和。

    def chain_setup(self, names):
        return [self.world_angle[n] % 360 for n in names]

    def chain_offsets(self, names, worlds, body_rot):
        offs, acc = [], body_rot
        for name, w in zip(names, worlds):
            off = w - self.world_angle[name] % 360 - acc
            offs.append(off)
            acc += off
        return offs

    # 冲刺（配合换图的通用攻击）：身体后仰压低蓄力 wind_lean → 冲出 dash、前倾 lean（命中在 dash_out 结束，默认 0.3 秒）→
    # 在 jabs 列出的各段伤害时刻往前一顶（jab 像素）→ dash_back 退回。head 部件跟着点头；arm_raise 给各条手臂第一节一个抬起角，
    # 蓄力时就能看出要动手。具体姿势通常交给 swap 换成原攻击图。
    def lunge(self, p):
        T = p.get("duration", 1.1)
        tm = dict({"wind": [0.0, 0.15], "dash_out": [0.15, 0.3], "dash_back": [0.8, 1.05]}, **p.get("timing", {}))
        dash, lean, wind_lean = p.get("dash", -40.0), p.get("lean", 6.0), p.get("wind_lean", -5.0)
        jabs, jab = p.get("jabs", []), p.get("jab", -10.0)
        head, arm_raise = p.get("head"), p.get("arm_raise", 0.0)
        # head_wind/head_out：head 部件在蓄力、冲出时的摆角（挂在丝上的茧往前荡就写负的 head_out）；tint：冲出期间染红的强度
        head_wind, head_out, tint = p.get("head_wind", -4.0), p.get("head_out", 5.0), p.get("tint", 0.0)

        def fn(t):
            w, o, b = seg(t, *tm["wind"]), seg(t, *tm["dash_out"]), seg(t, *tm["dash_back"])
            windup, out = w * (1 - o), o * (1 - b)
            kick = sum(math.exp(-((t - j) / 0.05) ** 2) for j in jabs)
            ch = {"body": {"rotate": wind_lean * windup + lean * out, "x": 8 * windup + dash * out + jab * kick,
                           "y": -4 * windup, "sx": 1 + 0.04 * windup - 0.02 * out, "sy": 1 - 0.04 * windup + 0.02 * out}}
            if head:
                ch[head] = {"rotate": head_wind * windup + head_out * out}
            if arm_raise:
                for limb in self.limbs:
                    if is_arm(limb):
                        ch[self.segs(limb)[0]] = {"rotate": side_sign(limb) * arm_raise * (windup + out)}
            return ch, {}

        def color(t):
            k = seg(t, *tm["wind"]) * (1 - seg(t, *tm["dash_back"]))
            return (1.0, 1 - tint * k, 1 - tint * k, 1.0)

        anim = self.build(fn, T, color if tint else None)
        if "fx" in p:
            fx = dict({"on": tm["dash_out"][0], "off": tm["dash_back"][0], "extra_spin": 0.0, "scale_from": 0.9,
                       "scale_to": 1.0, "fade_in": 0.05, "fade_out": 0.2, "scale_time": 0.12}, **p["fx"])
            self.add_spin_fx(anim, fx, T)
        return anim

    # 原地站着死：被打得一震、往后晃一下，再低头、身体微微往下垮并稍往前倾，停住（消失交给游戏）。不倒地。
    # head 是头部件名，droop 是低头角度，sag 是身体下沉像素，lean 是最后的前倾角度。
    def slump(self, p):
        T = p.get("duration", 1.2)
        head, droop, sag, lean = p.get("head"), p.get("droop", 14.0), p.get("sag", 8.0), p.get("lean", 4.0)

        def fn(t):
            jolt = seg(t, 0.0, 0.06) * (1 - seg(t, 0.12, 0.3))
            fall = seg(t, 0.2, 0.75)
            wob = math.exp(-6 * max(0.0, t - 0.75)) * math.sin(14 * max(0.0, t - 0.75)) * (t > 0.75)
            ch = {"body": {"rotate": -6 * jolt + lean * fall + 0.8 * wob, "x": 10 * jolt, "y": -sag * fall,
                           "sx": 1 + 0.02 * fall, "sy": 1 - 0.035 * fall}}
            if head:
                ch[head] = {"rotate": -5 * jolt + droop * fall + 1.5 * wob}
            return ch, {}

        flash = p.get("flash", 0.45)

        def color(t):
            k = seg(t, 0.0, 0.05) * (1 - seg(t, 0.08, 0.3))
            return (1.0, 1 - flash * k, 1 - flash * k, 1.0)

        return self.build(fn, T, color if flash else None)

    # 只动一个部件的关键帧动画（挂在丝上的蜘蛛巢等）：身体完全不动，挂点就固定在部件骨头上。
    # rotate/sx/sy 是 [[时间, 值], ...]，相邻关键帧之间平滑过渡；ring 是从 start 起带衰减的摆动（amp 度、freq 次/秒、decay）；
    # flash 是开头闪红强度，tint 是 [[时间, 强度], ...] 的持续染红；可带 fx。
    def part_keys(self, p):
        T = p.get("duration", 1.0)
        part = p["part"]

        def curve(keys, default):
            if not keys:
                return lambda t: default
            def f(t):
                if t <= keys[0][0]:
                    return keys[0][1]
                for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
                    if t <= t1:
                        return v0 + (v1 - v0) * smooth((t - t0) / (t1 - t0) if t1 > t0 else 1.0)
                return keys[-1][1]
            return f

        rot, sx, sy = curve(p.get("rotate"), 0.0), curve(p.get("sx"), 1.0), curve(p.get("sy"), 1.0)
        ring = p.get("ring")
        flash, tint = p.get("flash", 0.0), curve(p.get("tint"), 0.0)

        def fn(t):
            r = rot(t)
            if ring and t >= ring["start"]:
                dt = t - ring["start"]
                r += ring["amp"] * math.exp(-ring.get("decay", 5.0) * dt) * math.sin(2 * math.pi * ring.get("freq", 3.0) * dt)
            return {part: {"rotate": r, "sx": sx(t), "sy": sy(t)}}, {}

        def color(t):
            k = flash * seg(t, 0.0, 0.05) * (1 - seg(t, 0.08, 0.3)) + tint(t)
            return (1.0, 1 - k, 1 - k, 1.0)

        anim = self.build(fn, T, color if (flash or p.get("tint")) else None)
        if "fx" in p:
            fx = dict({"on": 0.15, "off": 0.8, "extra_spin": 0.0, "scale_from": 0.9, "scale_to": 1.0,
                       "fade_in": 0.05, "fade_out": 0.2, "scale_time": 0.12}, **p["fx"])
            self.add_spin_fx(anim, fx, T)
        return anim

    # 关节挥击：arm 链各节 抬到 raise → 劈到 strike（命中在 strike 结束，默认 0.3 秒）→ 收回；每往外一节晚 lag 秒，形成甩鞭的跟随。
    # 身体先后仰蓄力，劈下时前冲 lunge、前倾 lean；head 部件跟着点头。
    def joint_swing(self, p):
        T = p.get("duration", 1.15)
        tm = dict({"raise": [0.0, 0.16], "strike": [0.17, 0.3], "recover": [0.75, 1.1]}, **p.get("timing", {}))
        # chains：同时挥动的其他链（两条镰刀前肢等），每项 {arm, raise, strike}
        chains = [(p["arm"], p["raise"], p["strike"])] + [(c["arm"], c["raise"], c["strike"]) for c in p.get("chains", [])]
        lag = p.get("lag", 0.015)
        lunge, lean, head = p.get("lunge", -60.0), p.get("lean", 8.0), p.get("head")

        def fn(t):
            r, s, back = seg(t, *tm["raise"]), seg(t, *tm["strike"]), seg(t, *tm["recover"])
            windup, strike = r * (1 - s), s * (1 - back)
            body_rot = -0.7 * lean * windup + lean * strike
            ch = {"body": {"rotate": body_rot, "x": 10 * windup + lunge * strike, "y": -4 * windup,
                           "sx": 1 + 0.03 * windup, "sy": 1 - 0.03 * windup}}
            for arm, raise_w, strike_w in chains:
                names = self.segs(arm)
                worlds = []
                for i, (s0, a, b) in enumerate(zip(self.chain_setup(names), raise_w, strike_w)):
                    d = lag * i
                    ri = seg(t, tm["raise"][0] + d, tm["raise"][1] + d)
                    si = seg(t, tm["strike"][0] + d, tm["strike"][1] + d)
                    bi = seg(t, tm["recover"][0] + d, tm["recover"][1] + d)
                    worlds.append(s0 + (a - s0) * ri + (b - a) * si + (s0 - b) * bi)
                for name, off in zip(names, self.chain_offsets(names, worlds, body_rot)):
                    ch[name] = {"rotate": off}
            if head:
                ch[head] = {"rotate": -6 * windup + 8 * strike}
            return ch, {}

        anim = self.build(fn, T)
        if "fx" in p:
            fx = dict({"on": 0.2, "off": 0.6, "extra_spin": 0.0, "scale_from": 0.92, "scale_to": 1.0,
                       "fade_in": 0.06, "fade_out": 0.22, "scale_time": 0.1}, **p["fx"])
            self.add_spin_fx(anim, fx, T)
        return anim

    # 关节姿势（防御、施法、受击等）：arm 链各节摆到 pose 世界角，停住再放下；身体后仰 lean、平移 body_x，可选颤动 wobble 与闪红 flash
    def joint_pose(self, p):
        T = p.get("duration", 0.6)
        tm = dict({"up": [0.0, 0.14], "down": [0.42, 0.6]}, **p.get("timing", {}))
        # chains：同时摆姿势的其他链，每项 {arm, pose}
        chains = [(p["arm"], p["pose"])] + [(c["arm"], c["pose"]) for c in p.get("chains", [])]
        lean, head, lag = p.get("lean", -5.0), p.get("head"), p.get("lag", 0.02)
        # body_x：身体平移（受击时写正数就是被击退）；wobble：到位后各节带衰减的颤动（度）；flash：闪红强度
        body_x, wobble, flash = p.get("body_x", 6.0), p.get("wobble", 0.0), p.get("flash", 0.0)
        body_y = p.get("body_y", -3.0)

        def fn(t):
            k0 = seg(t, *tm["up"]) * (1 - seg(t, *tm["down"]))
            body_rot = lean * k0
            ch = {"body": {"rotate": body_rot, "x": body_x * k0, "y": body_y * k0, "sx": 1 + 0.02 * k0, "sy": 1 - 0.02 * k0}}
            shake = math.exp(-7 * max(0.0, t - tm["up"][1])) * math.sin(30 * t) * (t > tm["up"][0]) if wobble else 0.0
            for arm, pose in chains:
                names = self.segs(arm)
                worlds = []
                for i, (s0, a) in enumerate(zip(self.chain_setup(names), pose)):
                    d = lag * i
                    k = seg(t, tm["up"][0] + d, tm["up"][1] + d) * (1 - seg(t, tm["down"][0] + d, tm["down"][1] + d))
                    worlds.append(s0 + (a - s0) * k + wobble * (i + 1) * shake * k)
                for name, off in zip(names, self.chain_offsets(names, worlds, body_rot)):
                    ch[name] = {"rotate": off}
            if head:
                ch[head] = {"rotate": -5 * k0}
            return ch, {}

        def color(t):
            k = seg(t, 0.0, 0.05) * (1 - seg(t, 0.08, 0.3))
            return (1.0, 1 - flash * k, 1 - flash * k, 1.0)

        return self.build(fn, T, color if flash else None)

    # 头部甩出（求知的稻草人的收割等）：head 先后仰蓄力 wind_tilt，再往前甩 lash_tilt 并在停住时轻颤，身体跟着前倾 lean，
    # 两臂张开 arms 度；fx 一般挂在 head 上（fx.parent），从 scale_from 迅速放大，像从头里伸出去。
    # 命中在 lash 结束（默认 0.28 秒），多段伤害放在 lash 结束到 back 开始之间。
    def head_lash(self, p):
        T = p.get("duration", 1.2)
        tm = dict({"wind": [0.0, 0.16], "lash": [0.16, 0.28], "back": [0.85, 1.15]}, **p.get("timing", {}))
        head = p.get("head", "head")
        wind_tilt, lash_tilt = p.get("wind_tilt", -14.0), p.get("lash_tilt", 18.0)
        lean, arms = p.get("lean", 5.0), p.get("arms", 25.0)

        def fn(t):
            w, l, b = seg(t, *tm["wind"]), seg(t, *tm["lash"]), seg(t, *tm["back"])
            windup, lash = w * (1 - l), l * (1 - b)
            tremble = math.sin(48 * t) * lash
            ch = {"body": {"rotate": -0.6 * lean * windup + lean * lash, "x": 8 * windup - 16 * lash,
                           "sx": 1 + 0.02 * windup, "sy": 1 - 0.02 * windup},
                  head: {"rotate": wind_tilt * windup + lash_tilt * lash + 2.5 * tremble,
                         "sx": 1 + 0.05 * lash, "sy": 1 + 0.05 * lash}}
            for limb in self.limbs:
                if not is_arm(limb):
                    continue
                sign, names = side_sign(limb), self.segs(limb)
                ch[names[0]] = {"rotate": sign * arms * (0.4 * windup + lash) + 2 * tremble}
                if len(names) > 1:
                    ch[names[1]] = {"rotate": -sign * 0.6 * arms * lash}
            return ch, {}

        anim = self.build(fn, T)
        if "fx" in p:
            fx = dict({"on": tm["lash"][0] + 0.02, "off": tm["back"][0] + 0.1, "extra_spin": 0.0,
                       "scale_from": 0.15, "scale_to": 1.0, "fade_in": 0.04, "fade_out": 0.25, "scale_time": 0.12},
                      **p["fx"])
            self.add_spin_fx(anim, fx, T)
        return anim

    # 武器姿势（防御等）：weapon 举到目标世界角 angle、身体后仰 lean，停住再放下；角度同样换算成离初始角最近的等价角
    def weapon_pose(self, p):
        T = p.get("duration", 0.6)
        tm = dict({"up": [0.0, 0.12], "down": [0.42, 0.6]}, **p.get("timing", {}))
        names = self.segs(p["weapon"])
        rel = wrap(p["angle"] - self.world_angle[names[0]])
        lean, head = p.get("lean", -5.0), p.get("head")

        def fn(t):
            k = seg(t, *tm["up"]) * (1 - seg(t, *tm["down"]))
            ch = {"body": {"rotate": lean * k, "x": 6 * k, "y": -3 * k, "sx": 1 + 0.02 * k, "sy": 1 - 0.02 * k},
                  names[0]: {"rotate": (rel - lean) * k}}
            if head:
                ch[head] = {"rotate": -5 * k}
            return ch, {}

        return self.build(fn, T)

    # 挥武器攻击：weapon 肢体（第一节的父骨是 body）按目标世界角 抬起 raise_angle → 劈下 strike_angle（命中在 strike 结束，默认 0.3 秒）
    # → 收回；身体先后仰蓄力，劈下时前冲 lunge、前倾 lean。角度按数值直接插值，所以抬起、劈下的方向由数值大小决定（不绕最短路）。
    def swing_attack(self, p):
        T = p.get("duration", 1.0)
        tm = dict({"raise": [0.0, 0.16], "strike": [0.18, 0.3], "recover": [0.5, 0.9]}, **p.get("timing", {}))
        names = self.segs(p["weapon"])
        w0 = self.world_angle[names[0]]
        rel_raise, rel_strike = wrap(p["raise_angle"] - w0), wrap(p["strike_angle"] - w0)
        lunge, lean, head = p.get("lunge", -60.0), p.get("lean", 8.0), p.get("head")

        def fn(t):
            r, s, back = seg(t, *tm["raise"]), seg(t, *tm["strike"]), seg(t, *tm["recover"])
            windup, strike = r * (1 - s), s * (1 - back)
            body_rot = -0.7 * lean * windup + lean * strike
            ch = {"body": {"rotate": body_rot, "x": 10 * windup + lunge * strike, "y": -4 * windup,
                           "sx": 1 + 0.03 * windup, "sy": 1 - 0.03 * windup}}
            # 目标角换算成离初始角最近的等价角（骨头的初始角可能记成负数），再按数值插值
            rel = rel_raise * r + (rel_strike - rel_raise) * s - rel_strike * back
            ch[names[0]] = {"rotate": rel - body_rot}
            if head:
                ch[head] = {"rotate": -6 * windup + 8 * strike}
            return ch, {}

        anim = self.build(fn, T)
        if "fx" in p:
            fx = dict({"on": 0.2, "off": 0.6, "extra_spin": 0.0, "scale_from": 0.92, "scale_to": 1.0,
                       "fade_in": 0.06, "fade_out": 0.22, "scale_time": 0.1}, **p["fx"])
            self.add_spin_fx(anim, fx, T)
        return anim

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
        """特效图层：挂在 body 下的 fx 骨上，在 on–off 之间显示，淡入 fade_in、淡出 fade_out 秒，额外自转 extra_spin 度，
        scale_time 秒内从 scale_from 放大到 scale_to。"""
        on, off = fx.get("on", 0.34), fx.get("off", 1.0)
        extra = fx.get("extra_spin", 400.0)
        fade_in, fade_out, scale_time = fx.get("fade_in", 0.1), fx.get("fade_out", 0.16), fx.get("scale_time", 0.16)
        steps = round(T * FPS)
        rot, scale, rgba = [], [], []
        for i in range(steps + 1):
            t = T * i / steps
            a = seg(t, on, on + fade_in) * (1 - seg(t, off - fade_out, off))
            rot.append({"time": round(t, 4), "value": round(extra * smooth((t - on) / (off - on)), 3)})
            s = fx.get("scale_from", 0.82) + (fx.get("scale_to", 1.02) - fx.get("scale_from", 0.82)) * seg(t, on, on + scale_time)
            scale.append({"time": round(t, 4), "x": round(s, 4), "y": round(s, 4)})
            rgba.append({"time": round(t, 4), "color": f"ffffff{round(a * 255):02x}"})
        slot = fx.get("slot", "fx")
        anim["bones"][slot] = {"rotate": rot, "scale": scale}
        anim.setdefault("slots", {})[slot] = {
            "attachment": [{"time": round(on, 4), "name": slot}, {"time": off, "name": None}],
            "rgba": rgba,
        }

    def add_pose(self, anim, pose):
        """换图姿势：on–off 之间把分层身体淡出、换成原图里抠出的人物（pose_<动画名> 槽位，挂在 body 下，跟着身体的
        前冲、倾斜走），换入 fade_in、换回 fade_out 秒交叉淡入淡出（都缺省时取 fade）。动画已有的闪红等颜色关键帧会保留，并同样作用到换图上。"""
        T = max((k["time"] for tl in anim.get("bones", {}).values() for keys in tl.values() for k in keys), default=1.0)
        on, off = pose["on"], pose["off"]
        fade_in, fade_out = pose.get("fade_in", pose.get("fade", 0.05)), pose.get("fade_out", pose.get("fade", 0.05))
        slot = pose["slot"]
        steps = round(T * FPS)
        slots = anim.setdefault("slots", {})
        existing = slots.get(self.rig.slot_names[0], {}).get("rgba")
        layer_keys, pose_keys = [], []
        for i in range(steps + 1):
            t = T * i / steps
            vis = (seg(t, on, on + fade_in) if on > 0 else 1.0) * (1 - seg(t, off - fade_out, off))
            rgb, alpha = "ffffff", 1.0
            if existing:
                c = min(existing, key=lambda k: abs(k["time"] - t))["color"]
                rgb, alpha = c[:6], int(c[6:], 16) / 255
            layer_keys.append({"time": round(t, 4), "color": f"{rgb}{round(alpha * (1 - vis) * 255):02x}"})
            pose_keys.append({"time": round(t, 4), "color": f"{rgb}{round(alpha * vis * 255):02x}"})
        for name in self.rig.slot_names:
            slots.setdefault(name, {})["rgba"] = layer_keys
        slots[slot] = {"attachment": [{"time": round(on, 4), "name": slot}, {"time": round(off, 4), "name": None}],
                       "rgba": pose_keys}
        # pulse：在每段伤害时刻（times）让换图人物鼓一下（放大 amp，decay 秒内回落），多段招式只换一次图也能看出段数
        if pose.get("pulse"):
            pulse = pose["pulse"]
            amp, decay = pulse.get("amp", 0.05), pulse.get("decay", 0.12)
            keys = []
            for i in range(steps + 1):
                t = T * i / steps
                k = sum(clamp01((t - h + 0.03) / 0.03) * math.exp(-max(0.0, t - h) / decay) for h in pulse["times"])
                s = 1 + amp * k
                keys.append({"time": round(t, 4), "x": round(s, 4), "y": round(s, 4)})
            anim.setdefault("bones", {})[slot] = {"scale": keys}

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
    """从原攻击图抠特效，返回 (图集尺寸, 相对 fx 骨的偏移)。pivot（原图像素）对准 body 骨。
    key="red"（默认）按“红色占优”取像素；key="all" 保留全部不透明像素。
    exclude 挖掉角色本体：每项是 {"ellipse": [cx, cy, rx, ry]}、{"capsule": [x1, y1, x2, y2, r]} 或
    {"polygon": [[x, y], ...]}（原图像素，边缘 8 像素羽化）。
    inner_radius 再挖掉 pivot 周围一圈（red 默认 140，all 默认 0）。"""
    src = Image.open(fx["source"]).convert("RGBA")
    a = np.asarray(src).astype(np.float32)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    key = fx.get("key", "red")
    if key == "red":
        lo, span = fx.get("key_low", 25.0), fx.get("key_span", 70.0)
        k = np.clip(((r - np.maximum(g, b)) - lo) / span, 0, 1) * (al / 255)
    else:
        k = al / 255
    if fx.get("exclude"):
        yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]].astype(np.float32)
        for ex in fx["exclude"]:
            if "ellipse" in ex:
                cx, cy, rx, ry = ex["ellipse"]
                d = (np.hypot((xx - cx) / rx, (yy - cy) / ry) - 1) * min(rx, ry)
            elif "polygon" in ex:
                import cv2
                m = np.zeros(a.shape[:2], np.uint8)
                cv2.fillPoly(m, [np.array(ex["polygon"], np.int32)], 255)
                d = cv2.distanceTransform(255 - m, cv2.DIST_L2, 5) - cv2.distanceTransform(m, cv2.DIST_L2, 5)
            else:
                x1, y1, x2, y2, rr = ex["capsule"]
                vx, vy = x2 - x1, y2 - y1
                t = np.clip(((xx - x1) * vx + (yy - y1) * vy) / (vx * vx + vy * vy), 0, 1)
                d = np.hypot(xx - x1 - t * vx, yy - y1 - t * vy) - rr
            k = k * np.clip(d / 8, 0, 1)
    if fx.get("include"):
        # include：只保留这些多边形里的像素，用来从原图里抠出人物本体做换图姿势。羽化与 exclude 互补（向外 8 像素淡出），
        # 同一张原图用同一个多边形一边 include 出人物、一边 exclude 出特效，两层叠在一起时切口看不出接缝。
        import cv2
        m = np.zeros(a.shape[:2], np.uint8)
        for poly in fx["include"]:
            cv2.fillPoly(m, [np.array(poly, np.int32)], 255)
        k = k * (1 - np.clip(cv2.distanceTransform(255 - m, cv2.DIST_L2, 5) / 8, 0, 1))
    out = np.zeros_like(a)
    out[..., :3] = a[..., :3]
    out[..., 3] = 255 * k
    img = Image.fromarray(out.astype(np.uint8), "RGBA")
    box = img.getchannel("A").point(lambda v: 255 if v > 10 else 0).getbbox()
    crop = np.asarray(img.crop(box)).copy()
    px, py = fx["pivot"][0] - box[0], fx["pivot"][1] - box[1]
    inner = fx.get("inner_radius", 140.0 if key == "red" else 0.0)
    if inner > 0:
        yy, xx = np.mgrid[0:crop.shape[0], 0:crop.shape[1]]
        dist = np.hypot(xx - px, yy - py)
        crop[..., 3] = (crop[..., 3] * np.clip((dist - inner) / 20, 0, 1)).astype(np.uint8)
    fx_img = Image.fromarray(crop, "RGBA")
    fx_img.save(out_dir / page)
    w, h = fx_img.size
    return (w, h), (w / 2 - px, py - h / 2)


def visible_bottom(path):
    a = np.asarray(Image.open(path).convert("RGBA"))[..., 3]
    rows = np.nonzero(a.max(axis=1) > 0)[0]
    return float(rows.max() + 1) if len(rows) else float(a.shape[0])


def align_swap(pose, rig, cfg):
    """按原版贴图外观的换图规则算 anchor 与 scale，换图位置与原来逐帧换图时一致。
    align = {"mode": "center" | "bottom", "base_scale": 待机贴图缩放, "frame_scale": 换图帧缩放（缺省同待机）,
             "nudge": [x, y]（原版 Frame.Nudge，父节点单位；Lunge 帧的 Nudge 是冲刺终点，不要填）}。
    center 对应 SpriteVisualProfile.Centered()（图片中心对齐待机图中心），bottom 对应默认的可见底边中点对齐。"""
    al = pose["align"]
    base = al["base_scale"]
    s = al.get("frame_scale", base) / base
    nx, ny = (v / base for v in al.get("nudge", [0.0, 0.0]))
    src = Image.open(pose["source"])
    w, h = src.size
    W, H = rig.W, rig.H
    if al.get("mode", "bottom") == "center":
        q = (w / 2, h / 2)
        p = (W / 2 + nx, H / 2 + ny)
    else:
        q = (w / 2, visible_bottom(pose["source"]))
        p = (W / 2 + nx, visible_bottom(cfg["image"]) + ny)
    bx, by = cfg["body"]["center"]
    pose["anchor"] = [round(q[0] - (p[0] - bx) / s, 2), round(q[1] - (p[1] - by) / s, 2)]
    if abs(s - 1.0) > 1e-6:
        pose["scale"] = round(s, 4)


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
        for key in ("fx", "swap"):
            if key in anim:
                anim[key]["source"] = str((cfg_path.parent / anim[key]["source"]).resolve())

    rig = Rig(cfg, out_dir)
    rig.build_bones()
    name, W, H = rig.name, rig.W, rig.H
    if "layers" in cfg:
        # 分层：每层一个槽位（按配置顺序从后到前）、一个网格附件、一页图集；附件与区域名都用 <name>_<层名>
        layer_meshes = rig.build_layers()
        # behind=true 的层画在底层后面（例如从躯干两侧伸出、根部藏在领结后的手臂）
        behind = {l["name"] for l in cfg["layers"][1:] if l.get("behind")}
        layer_meshes = [m for m in layer_meshes if m[0] in behind] + [m for m in layer_meshes if m[0] not in behind]
        slots, attachments, atlas_pages = [], {}, []
        for layer_name, page, mesh, _ in layer_meshes:
            region = f"{name}_{layer_name}"
            slots.append({"name": region, "bone": "root", "attachment": region})
            attachments[region] = {region: mesh}
            atlas_pages.append(f"{page}\nsize:{W},{H}\nfilter:Linear,Linear\n{region}\nbounds:0,0,{W},{H}\n")
        rig.slot_names = [s["name"] for s in slots]
        rig.vertex_count = sum(c for *_, c in layer_meshes)
        tri_count = sum(len(m["triangles"]) // 3 for _, _, m, _ in layer_meshes)
        atlas = "\n".join(atlas_pages)
    else:
        rig.build_mesh()
        rig.slot_names = [name]
        slots = [{"name": name, "bone": "root", "attachment": name}]
        attachments = {name: {name: rig.mesh}}
        tri_count = len(rig.mesh["triangles"]) // 3
        atlas = f"{rig.page}\nsize:{W},{H}\nfilter:Linear,Linear\n{name}\nbounds:0,0,{W},{H}\n"
    data = {
        "skeleton": {"hash": f"tier1-{name}", "spine": "4.2.00", "x": -W / 2, "y": 0, "width": W, "height": H,
                     "images": "./", "audio": ""},
        "bones": rig.bones,
        "slots": slots,
        "ik": rig.ik(),
        "skins": [{"name": "default", "attachments": attachments}],
        "animations": {},
    }

    anims = cfg.get("animations", {})
    # 每个带 fx 的动画各一个特效槽位：第一个叫 fx（页面默认 fx.png），之后的叫 fx_<动画名>（页面默认 fx_<动画名>.png）。
    # 骨头默认挂在 body 下，parent 可改挂到别的骨头（例如从头部甩出的特效挂 head，pivot 对准那根骨头）。
    for anim_name, a in anims.items():
        if "fx" not in a:
            continue
        fx_cfg = a["fx"]
        slot = "fx" if not any(s["name"] == "fx" for s in data["slots"]) else f"fx_{anim_name}"
        fx_cfg["slot"] = slot
        fx_page = fx_cfg.get("page", f"{slot}.png")
        if "align" in fx_cfg:
            # 特效放在原版逐帧换图时那张攻击图的位置上：pivot、scale 与 swap 的 align 同一套算法
            probe = {"source": fx_cfg["source"], "align": fx_cfg["align"]}
            align_swap(probe, rig, cfg)
            fx_cfg["pivot"] = probe["anchor"]
            fx_cfg["scale"] = probe.get("scale", 1.0)
        (fw, fh), (ox, oy) = extract_fx(fx_cfg, out_dir, fx_page)
        fx_scale = fx_cfg.get("scale", 1.0)
        bone = {"name": slot, "parent": fx_cfg.get("parent", "body")}
        # origin（原图像素）：特效骨放在这一点上，缩放、自转以它为中心（例如从头里伸出的茎以茎根为中心放大）
        bx = by = 0.0
        if "origin" in fx_cfg:
            bx = (fx_cfg["origin"][0] - fx_cfg["pivot"][0]) * fx_scale
            by = (fx_cfg["pivot"][1] - fx_cfg["origin"][1]) * fx_scale
            bone.update(x=round(bx, 2), y=round(by, 2))
        data["bones"].append(bone)
        data["slots"].insert(0, {"name": slot, "bone": slot})  # 排在本体后面，初始不显示
        # 攻击图与待机图画幅比例不同时按 scale 缩放特效（偏移一起缩放，pivot 仍对准 body）
        attachment = {"x": ox, "y": oy, "width": fw, "height": fh}
        if fx_scale != 1.0 or bx or by:
            attachment.update(x=round(ox * fx_scale - bx, 3), y=round(oy * fx_scale - by, 3))
        if fx_scale != 1.0:
            attachment.update(scaleX=fx_scale, scaleY=fx_scale)
        data["skins"][0]["attachments"][slot] = {slot: attachment}
        atlas += f"\n{fx_page}\nsize:{fw},{fh}\nfilter:Linear,Linear\n{slot}\nbounds:0,0,{fw},{fh}\n"

    # 换图（swap）：source 是原攻击/受击图，include 圈出人物本体，anchor 是人物脚下对应 body 骨的点（原图像素）。
    # 每个带 swap 的动画一个槽位 pose_<动画名>，排在最前面，初始不显示。
    for anim_name, a in anims.items():
        if "swap" not in a:
            continue
        pose = a["swap"]
        if "align" in pose:
            align_swap(pose, rig, cfg)
        slot = pose["slot"] = f"pose_{anim_name}"
        if pose.get("reuse_page"):
            # 整张原图不抠：图集页直接引用原贴图（须与图集同目录），不另存一份 PNG
            page = Path(pose["source"]).name
            pw, ph = Image.open(pose["source"]).size
            ax, ay = pose["anchor"]
            ox, oy = pw / 2 - ax, ay - ph / 2
        else:
            page = pose.get("page", f"{name}_pose_{anim_name}.png")
            spec = {"source": pose["source"], "key": "all", "include": pose.get("include"), "exclude": pose.get("exclude"),
                    "pivot": pose["anchor"], "inner_radius": 0.0}
            (pw, ph), (ox, oy) = extract_fx(spec, out_dir, page)
        data["bones"].append({"name": slot, "parent": "body"})
        data["slots"].append({"name": slot, "bone": slot})
        att = {"x": ox, "y": oy, "width": pw, "height": ph}
        # scale：原版换图另给了缩放的（例如攻击图比待机图画得小）照抄，anchor 仍对准 body 骨
        if pose.get("scale", 1.0) != 1.0:
            s = pose["scale"]
            att.update(x=round(ox * s, 3), y=round(oy * s, 3), scaleX=s, scaleY=s)
        data["skins"][0]["attachments"][slot] = {slot: att}
        atlas += f"\n{page}\nsize:{pw},{ph}\nfilter:Linear,Linear\n{slot}\nbounds:0,0,{pw},{ph}\n"

    animator = Animator(rig, data)
    for anim_name, p in anims.items():
        kind = p.get("type", anim_name)
        data["animations"][anim_name] = getattr(animator, kind)(p)
        if "swap" in p:
            animator.add_pose(data["animations"][anim_name], p["swap"])

    (out_dir / f"{name}.spine-json").write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
    (out_dir / f"{name}.atlas").write_text(atlas, encoding="utf-8")
    print(f"{name}: bones={len(data['bones'])} verts={rig.vertex_count} tris={tri_count} "
          f"ik={[c['bendPositive'] for c in data['ik']]} anims={list(data['animations'])} -> {out_dir}")


if __name__ == "__main__":
    main()
