"""读场景动画库（*_animations.tres）里每个动作的换图时间线：AttackVisuals 的贴图在哪些时刻换成哪张、动作总长。
文学层的 Boss 用场景动画（SceneAnimatedCreatureVisuals），怪物代码按动画契约里的命中帧时刻结算伤害，
骨骼版要照这里的时间点换姿势。
用法：scene_timelines.py <动画库.tres> → 打印 JSON {动作: {"length": 秒, "frames": [[时刻, 贴图路径], ...]}}"""
import json
import re
import sys
from pathlib import Path

text = Path(sys.argv[1]).read_text(encoding="utf-8")
ext = {i: p for p, i in re.findall(r'\[ext_resource type="Texture2D"[^\]]*path="([^"]+)" id="([^"]+)"\]', text)}
out = {}
for block in re.split(r'\n(?=\[sub_resource type="Animation")', text):
    # 场景里内嵌的动画库（失乐园）没有 resource_name，用子资源 id（anim_<库>_<动作>）
    name = (re.search(r'resource_name = "([^"]+)"', block)
            or re.search(r'\[sub_resource type="Animation" id="([^"]+)"\]', block))
    if not name:
        continue
    length = re.search(r"\nlength = ([0-9.]+)", block)
    entry = {"length": float(length.group(1)) if length else 0.0, "frames": []}
    for track in re.split(r"\ntracks/\d+/type", block):
        if 'AttackVisuals:texture")' not in track and 'Visuals:texture")' not in track:
            continue
        if "AttackVisuals" not in track and entry["frames"]:
            continue
        times = [float(v) for v in re.search(r'"times": PackedFloat32Array\(([^)]*)\)', track).group(1).split(",")]
        ids = re.findall(r'ExtResource\("([^"]+)"\)', re.search(r'"values": \[([^\]]*)\]', track).group(1))
        entry["frames"] = [[t, ext[i]] for t, i in zip(times, ids)]
    out[name.group(1)] = entry
print(json.dumps(out, ensure_ascii=False, indent=1))
