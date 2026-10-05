"""盘点原版人物包：每个预制体有几个动作（CharacterMotion）、每个动作几层 SpriteRenderer、有没有 Animator / 动画片段，
用来判断哪些人物有分层素材可用。输出 JSON：{包名: {prefab, motions: {动作: 层数}, animators, clips}}。
用法：survey_bundles.py <输出.json>"""
import json
import sys
from pathlib import Path

import UnityPy

G = Path.home() / ".local/share/LibraryOfRuina-win/LibraryOfRuina_Data/StreamingAssets/AssetBundles/char"


def pid(p):
    return getattr(p, "path_id", None) or getattr(p, "m_PathID", 0)


out = {}
for f in sorted(G.glob("*.pres"), key=lambda p: int(p.stem.split("_")[1])):
    env = UnityPy.load(str(f))
    by_id = {o.path_id: o for o in env.objects}
    motions = {}
    animators = 0
    clips = 0
    loose_renderers = 0
    for o in env.objects:
        t = o.type.name
        if t == "Animator":
            animators += 1
        elif t == "AnimationClip":
            clips += 1
        elif t == "SpriteRenderer":
            loose_renderers += 1
        elif t == "MonoBehaviour":
            try:
                mb = o.read()
                s = by_id.get(pid(mb.m_Script))
                if not s or s.read().m_ClassName != "CharacterMotion":
                    continue
                tree = o.read_typetree()
                name = by_id[pid(mb.m_GameObject)].read().m_Name
                motions[name] = len(tree.get("motionSpriteSet", []))
            except Exception:
                continue
    out[f.stem] = {"prefab": sorted(env.container.keys()), "motions": motions, "animators": animators,
                   "clips": clips, "renderers": loose_renderers}
Path(sys.argv[1]).write_text(json.dumps(out, ensure_ascii=False, indent=1))
print(len(out))
