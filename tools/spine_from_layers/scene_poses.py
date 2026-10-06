"""读场景动画库（*_animations.tres）里每个动作各张贴图的摆放：贴图路径、Sprite2D 的 position / offset / scale、
MotionRoot 的位置，以及在动作里出现的时刻。整图怪物里原来是场景动画的（自然层隐士之杖、闪耀的幸福等）
用它把每张贴图放进 sprite_layers.py 的同一坐标系。场景节点的初值（没有轨道时）从 .tscn 读。
用法：scene_poses.py <场景.tscn> <动画库.tres>... → 打印 JSON {库: {动作: [{time, node, texture, position, offset, scale}]}}"""
import json
import re
import sys
from pathlib import Path

R = Path(__file__).resolve().parents[2]


def vec(s):
    m = re.match(r"Vector2\(([-0-9.e]+),\s*([-0-9.e]+)\)", s.strip())
    return [float(m.group(1)), float(m.group(2))] if m else None


def parse_values(raw):
    """轨道的 values 数组：Vector2(...)、ExtResource("id")、true/false。"""
    out = []
    for tok in re.findall(r'Vector2\([^)]*\)|ExtResource\("[^"]+"\)|true|false', raw):
        if tok.startswith("Vector2"):
            out.append(vec(tok))
        elif tok.startswith("ExtResource"):
            out.append(re.search(r'"([^"]+)"', tok).group(1))
        else:
            out.append(tok == "true")
    return out


def ext_map(text):
    return {i: p for p, i in re.findall(r'\[ext_resource type="Texture2D"[^\]]*path="([^"]+)" id="([^"]+)"\]', text)}


def scene_defaults(tscn):
    text = Path(tscn).read_text(encoding="utf-8")
    ext = ext_map(text)
    nodes = {}
    for block in re.split(r"\n(?=\[node )", text):
        m = re.match(r'\[node name="([^"]+)"[^\]]*?(?:parent="([^"]*)")?\]', block)
        if not m:
            continue
        name, parent = m.group(1), m.group(2)
        path = name if parent in (None, ".") else f"{parent}/{name}"
        d = {}
        for key in ("position", "offset", "scale"):
            km = re.search(rf"\n{key} = (Vector2\([^)]*\))", block)
            if km:
                d[key] = vec(km.group(1))
        tm = re.search(r'\ntexture = ExtResource\("([^"]+)"\)', block)
        if tm:
            d["texture"] = ext.get(tm.group(1))
        cm = re.search(r"\ncentered = (true|false)", block)
        d["centered"] = cm.group(1) == "true" if cm else True
        nodes[path] = d
    return nodes


def poses(tscn, tres_list):
    base = scene_defaults(tscn)
    result = {}
    for tres in tres_list:
        text = Path(tres).read_text(encoding="utf-8")
        ext = ext_map(text)
        lib = {}
        for block in re.split(r'\n(?=\[sub_resource type="Animation")', text):
            nm = re.search(r'resource_name = "([^"]+)"', block)
            if not nm:
                continue
            tracks = {}
            for tr in re.split(r"\ntracks/\d+/type", block)[1:]:
                pm = re.search(r'path = NodePath\("([^"]+)"\)', tr)
                tm = re.search(r'"times": PackedFloat32Array\(([^)]*)\)', tr)
                vm = re.search(r'"values": \[(.*?)\]\n', tr, re.S)
                if not (pm and tm and vm):
                    continue
                times = [float(t) for t in tm.group(1).split(",") if t.strip()]
                vals = parse_values(vm.group(1))
                vals = [ext.get(v, v) if isinstance(v, str) else v for v in vals]
                tracks[pm.group(1)] = list(zip(times, vals))
            root_pos = (tracks.get("MotionRoot:position") or [(0, [0.0, 0.0])])[0][1]
            shots = []
            for node in ("MotionRoot/Visuals", "MotionRoot/AttackVisuals"):
                vis = tracks.get(f"{node}:visible")
                tex = tracks.get(f"{node}:texture") or [(0.0, base.get(node, {}).get("texture"))]
                for t, texture in tex:
                    shown = True
                    if vis:
                        shown = [v for tt, v in vis if tt <= t + 1e-6][-1] if any(tt <= t + 1e-6 for tt, v in vis) else vis[0][1]
                    if not shown or not texture:
                        continue

                    def at(key):
                        tr = tracks.get(f"{node}:{key}")
                        if tr:
                            return [v for tt, v in tr if tt <= t + 1e-6][-1] if any(tt <= t + 1e-6 for tt, v in tr) else tr[0][1]
                        return base.get(node, {}).get(key, [1.0, 1.0] if key == "scale" else [0.0, 0.0])

                    shots.append({"time": t, "node": node.split("/")[-1], "texture": texture,
                                  "position": [at("position")[0] + root_pos[0], at("position")[1] + root_pos[1]],
                                  "offset": at("offset"), "scale": at("scale"),
                                  "centered": base.get(node, {}).get("centered", True)})
            lib[nm.group(1)] = sorted(shots, key=lambda s: s["time"])
        result[Path(tres).stem] = lib
    return result


if __name__ == "__main__":
    print(json.dumps(poses(sys.argv[1], sys.argv[2:]), ensure_ascii=False, indent=1))
