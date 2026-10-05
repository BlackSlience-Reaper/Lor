"""模组图叠了特效、match_images 对不上时，逐个原版动作跑 extract_fx（只比合成图不透明的部分），按误差列前三，
抠出的特效存在 <输出目录>/<贴图名>__<动作>.png，挑中的那张拷进 fx/。
用法：fx_search.py <分层目录> <输出目录> <贴图>..."""
import json
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

HERE = Path(__file__).resolve().parent
root, out_dir, images = Path(sys.argv[1]).expanduser(), Path(sys.argv[2]), [Path(p) for p in sys.argv[3:]]
out_dir.mkdir(parents=True, exist_ok=True)
motions = list(json.loads((root / "layers.json").read_text()))


def run(args):
    img, m = args
    out = out_dir / f"{img.stem}__{m}.png"
    r = subprocess.run(["python3", str(HERE / "extract_fx.py"), str(root), m, str(img), str(out)], capture_output=True, text=True)
    try:
        return img, m, json.loads(r.stdout.strip().splitlines()[-1])
    except (IndexError, json.JSONDecodeError):
        return img, m, {"err": float("inf")}


res = {}
with ThreadPoolExecutor(6) as ex:
    for img, m, r in ex.map(run, [(i, m) for i in images for m in motions]):
        res.setdefault(img.name, []).append((r["err"], m, r))
for name, rs in res.items():
    rs.sort(key=lambda v: v[0])
    print(name, " | ".join(f"{m} {e:.1f} c={r.get('center')}" for e, m, r in rs[:3]), flush=True)
