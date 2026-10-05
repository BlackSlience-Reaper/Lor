"""批量：把模组来宾的待机/攻击/受击图各对上原版哪个动作（alpha 吻合度，先缩小粗搜再原尺寸精搜），
得到骨架原点（模组待机图底边中点在原版角色坐标里的位置）与肤色。
用法：match_all.py <分层根目录> <模组 images/monsters> <输出 json> 名字=分层子目录[,模组前缀] ...
分层子目录由 export_layers.py 导出；模组前缀缺省同名字（铁之兄弟写 brotherhood_of_iron/arnold）。"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

SUFFIX = {"idle": "", "strike": "_attack_strike", "thrust": "_attack_thrust", "slash": "_attack_slash", "hurt": "_hit"}


def find_mod(mod_root, prefix, kind):
    for ext in (".webp", ".png"):
        p = mod_root / f"{prefix}{SUFFIX[kind]}{ext}"
        if p.exists():
            return p
    return None


def alpha(path, scale=1.0):
    im = Image.open(path).convert("RGBA")
    if scale != 1.0:
        im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.BILINEAR)
    return np.asarray(im.getchannel("A"), dtype=np.float32)


def search(comp, mod, dxs, dys):
    """comp 里放 mod，返回 (平均误差, dx, dy)；comp 已按需要四周补零。"""
    best = (1e9, 0, 0)
    H, W = mod.shape
    for dy in dys:
        for dx in dxs:
            if dy < 0 or dx < 0 or dy + H > comp.shape[0] or dx + W > comp.shape[1]:
                continue
            e = float(np.abs(comp[dy:dy + H, dx:dx + W] - mod).mean())
            if e < best[0]:
                best = (e, dx, dy)
    return best


def match(comp_path, mod_path):
    PAD = 40
    S = 0.25
    cs, ms = alpha(comp_path, S), alpha(mod_path, S)
    p = round(PAD * S)
    cs = np.pad(cs, ((p, p + ms.shape[0]), (p, p + ms.shape[1])))
    e, dx, dy = search(cs, ms, range(cs.shape[1] - ms.shape[1] + 1), range(cs.shape[0] - ms.shape[0] + 1))
    c, m = alpha(comp_path), alpha(mod_path)
    c = np.pad(c, ((PAD, PAD + m.shape[0]), (PAD, PAD + m.shape[1])))
    gx, gy = round(dx / S), round(dy / S)
    e, dx, dy = search(c, m, range(gx - 6, gx + 7), range(gy - 6, gy + 7))
    return e, dx - PAD, dy - PAD


def main():
    layers_root, mod_root, dst = Path(sys.argv[1]).expanduser(), Path(sys.argv[2]), Path(sys.argv[3])
    result = {}
    for arg in sys.argv[4:]:
        name, rest = arg.split("=")
        sub, _, prefix = rest.partition(",")
        prefix = prefix or name
        info = json.loads((layers_root / sub / "layers.json").read_text())
        motions = [m for m in info if not m.endswith("_Standing")]
        entry = {"layers": sub, "prefix": prefix, "poses": {}}
        for kind in SUFFIX:
            mp = find_mod(mod_root, prefix, kind)
            if mp is None:
                continue
            cands = []
            for m in motions:
                e, dx, dy = match(layers_root / sub / f"{m}.composite.png", mp)
                cands.append((e, m, dx, dy))
            cands.sort()
            e, m, dx, dy = cands[0]
            W, H = Image.open(mp).size
            ox, oy = info[m]["composite_origin"]
            bottom = [round(dx + W / 2 - ox, 2), round(oy - (dy + H), 2)]
            entry["poses"][kind] = {"motion": m, "err": round(e, 3), "second": [round(cands[1][0], 3), cands[1][1]],
                                    "mod": mp.name, "bottom_center": bottom, "size": [W, H]}
            print(name, kind, m, round(e, 2), "next", cands[1][1], round(cands[1][0], 2), bottom, flush=True)
        result[name] = entry
    dst.write_text(json.dumps(result, ensure_ascii=False, indent=1))


# 只在直接运行时执行：type_tints.py 等会导入 match()，导入时跑主流程曾把命令行里的模组贴图当输出路径覆盖掉
if __name__ == "__main__":
    main()
