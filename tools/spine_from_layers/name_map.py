"""把贴图路径片段对回模组怪物：在 src 里找引用该路径的外观类，取它 [MonsterVisual(typeof(X))] 的怪物类型、
外观基类，以及 zhs 本地化里的名字。用法：name_map.py 路径片段...（如 technology_floor/chord）"""
import json
import re
import sys
from pathlib import Path

R = Path("/Users/iniad/LibraryOfRuina")
loc = json.loads((R / "LibraryOfRuina/localization/zhs/monsters.json").read_text(encoding="utf-8"))
files = list((R / "src").rglob("*.cs"))
texts = {f: f.read_text(encoding="utf-8") for f in files}


def snake(name):
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).upper()


for frag in sys.argv[1:]:
    hits = [f for f, t in texts.items() if frag in t]
    monsters = set()
    bases = set()
    for f in hits:
        t = texts[f]
        monsters.update(re.findall(r"MonsterVisual\(typeof\((\w+)\)", t))
        bases.update(re.findall(r"class \w+\s*:\s*(\w+)", t))
        # 资源常量写在怪物类里时，按类名找外观
        for cls in re.findall(r"class (\w+)\b", t):
            for g, tt in texts.items():
                if f"typeof({cls})" in tt and "MonsterVisual" in tt:
                    monsters.add(cls)
                    bases.update(re.findall(r"class \w+\s*:\s*(\w+)", tt))
    names = [f"{m}={loc.get(snake(m) + '.name', '?')}" for m in sorted(monsters)]
    print(frag, "|", ", ".join(names) or "-", "|", ",".join(sorted(b for b in bases if "Visuals" in b)) or "-")
