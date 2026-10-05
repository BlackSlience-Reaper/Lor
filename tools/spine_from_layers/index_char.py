"""给 AssetBundles/char 下 736 个人物包建名字索引：包名、容器路径、贴图名。"""
import json
from pathlib import Path

import UnityPy

G = Path.home() / ".local/share/LibraryOfRuina-win/LibraryOfRuina_Data/StreamingAssets/AssetBundles/char"
out = {}
for f in sorted(G.glob("*.pres"), key=lambda p: int(p.stem.split("_")[1])):
    env = UnityPy.load(str(f))
    entry = {"container": sorted(env.container.keys()), "textures": [], "clips": []}
    for obj in env.objects:
        if obj.type.name == "Texture2D":
            entry["textures"].append(obj.peek_name())
        elif obj.type.name == "AnimationClip":
            entry["clips"].append(obj.peek_name())
    out[f.stem] = entry
Path(__file__).with_name("char_index.json").write_text(json.dumps(out, ensure_ascii=False, indent=1))
print(len(out))
