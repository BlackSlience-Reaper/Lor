"""去掉部分层（模组图里没有的饰物、叠在一起的另几张表情图）后再把模组贴图对到原版动作，可同时按倍数缩放。
打印误差与骨架原点（贴图底边中点在原版坐标里的位置，原版单位），用于配置里带 drop 的姿势。
用法：match_dropped.py <分层目录> <动作> <去掉的层,逗号分隔|-> <倍数> <贴图>..."""
import json
import sys
import tempfile
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
from match_all import match  # noqa: E402



def composite(root, motion, drop):
    """按 export_layers 的同一画布与原点重新合成，跳过 drop 里的层。"""
    info = json.loads((root / "layers.json").read_text())[motion]
    ox, oy = info["composite_origin"]
    canvas = Image.new("RGBA", Image.open(root / f"{motion}.composite.png").size)
    for l in info["layers"]:
        if l["name"] in drop:
            continue
        im = Image.open(root / motion / f"{l['name']}.png").convert("RGBA")
        sx, sy = l["scale"][0] * 100.0 / (l.get("ppu") or 100.0), l["scale"][1] * 100.0 / (l.get("ppu") or 100.0)
        if abs(abs(sx) - 1) > 1e-3 or abs(abs(sy) - 1) > 1e-3:
            im = im.resize((max(1, round(im.width * abs(sx))), max(1, round(im.height * abs(sy)))), Image.LANCZOS)
        if sx < 0:
            im = im.transpose(Image.FLIP_LEFT_RIGHT)
        if sy < 0:
            im = im.transpose(Image.FLIP_TOP_BOTTOM)
        if abs(l["rot"]) > 1e-3:
            im = im.rotate(l["rot"], resample=Image.BICUBIC, expand=True)
        cx, cy = l["center"]
        canvas.alpha_composite(im, (round(cx - im.width / 2 + ox), round(oy - cy - im.height / 2)))
    return canvas


if __name__ == "__main__":
    root, motion, drop, k = Path(sys.argv[1]).expanduser(), sys.argv[2], sys.argv[3], float(sys.argv[4])
    drop = set() if drop == "-" else set(drop.split(","))
    ox, oy = json.loads((root / "layers.json").read_text())[motion]["composite_origin"]
    canvas = composite(root, motion, drop)
    if k != 1.0:
        canvas = canvas.resize((round(canvas.width * k), round(canvas.height * k)), Image.LANCZOS)
    tmp = Path(tempfile.mkdtemp()) / "composite.png"
    canvas.save(tmp)
    for img in sys.argv[5:]:
        p = Path(img)
        e, dx, dy = match(tmp, p)
        W, H = Image.open(p).size
        print(f"{p.name}: {motion} -{','.join(sorted(drop)) or '无'} ×{k} err={e:.2f} | bottom_center="
              f"[{(dx + W / 2) / k - ox:.1f}, {oy - (dy + H) / k:.1f}]", flush=True)
