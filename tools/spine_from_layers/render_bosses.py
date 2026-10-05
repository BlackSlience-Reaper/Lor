"""生成并渲染 boss_<名字>.json，拼成一张多格 GIF（每格各自按触发顺序连播，短的循环补齐）。
用法：render_bosses.py <输出.gif> 名字..."""
import json
import os
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

R = Path("/Users/iniad/LibraryOfRuina")
HERE = Path(__file__).resolve().parent
RENDER = Path.home() / ".claude/skills/sts2-spine-from-sprite/scripts/render.sh"
FEET_Y = 860
TITLES = {"dacapo": "Da Capo", "little_galaxy": "我们的小小银河", "nostalgic_scent": "余香", "pleasure": "欢愉", "beyond_fragment": "彼方的碎片", "chord": "和弦", "grinder_mk4": "研削机Mk4", "magic_bullet": "魔弹", "regret": "悔恨", "solemn_mourning": "庄严哀悼"}
ACT = {"idle": "待机", "attack": "攻击", "attack_fire": "远程", "attack_strike": "打击", "attack_slash": "斩击",
       "attack_thrust": "突刺", "attack_right": "右击", "attack_left": "左击", "hurt": "受击", "guard": "防御",
       "parry": "格挡", "dodge": "闪避", "ego": "E.G.O", "ego_s1": "E.G.O 1", "ego_s2": "E.G.O 2", "ego_s3": "E.G.O 3",
       "special": "特殊", "die": "死亡", "ranged": "远程", "blunt": "打击", "pierce": "突刺", "slash": "斩击", "attack2": "攻击 2"}


def frames_dir(n):
    return Path(f"/tmp/spine_from_sprite_frames/boss_{n}")


def render(n):
    cfg_path = HERE / f"boss_{n}.json"
    cfg = json.loads(cfg_path.read_text())
    out = R / "build/boss_layers" / n
    subprocess.run(["python3", str(HERE / "spine_from_layers.py"), str(cfg_path), "--out", str(out / "out")], check=True)
    plan = [["idle", True, 36]]
    for a, spec in cfg["animations"].items():
        if a == "idle":
            continue
        plan.append([a, False, round(spec["duration"] * 30) + 2])
        plan.append(["idle", True, 10])
    rj = {"name": cfg["name"], "out_dir": str(out / "out"), "frames_dir": str(frames_dir(n)), "fps": 30, "mix": 0.12,
          "viewport": [1600, 1100], "position": [900, FEET_Y - cfg["origin"][1]], "plan": plan}
    (out / "render.json").write_text(json.dumps(rj))
    r = subprocess.run(["bash", str(RENDER), str(out / "render.json")], capture_output=True, text=True)
    print(n, r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr[-300:], flush=True)


def compose(names, dst):
    font = ImageFont.truetype("/System/Library/Fonts/Hiragino Sans GB.ttc", 18)
    crop = (100, 0, 1500, 1000)  # 高个子（光环、头冠）不被切掉
    tw, th = (int(v) for v in os.environ.get("TILE", "392x280").split("x"))
    step = int(os.environ.get("STEP", "2"))  # 默认抽成每秒 15 帧，控制 GIF 体积
    cols = 3
    rows = (len(names) + cols - 1) // cols
    seqs = {n: (sorted(frames_dir(n).glob("f_*.png")), (frames_dir(n) / "labels.txt").read_text().splitlines()) for n in names}
    total = max(len(f) for f, _ in seqs.values())
    out = []
    for i in range(0, total, step):
        sheet = Image.new("RGBA", (cols * tw, rows * th), (36, 34, 42, 255))
        d = ImageDraw.Draw(sheet)
        for k, n in enumerate(names):
            files, labels = seqs[n]
            j = i % len(files)
            im = Image.open(files[j]).convert("RGBA").crop(crop).resize((tw, th), Image.LANCZOS)
            x, y = (k % cols) * tw, (k // cols) * th
            gy = y + round((FEET_Y - crop[1]) * th / (crop[3] - crop[1]))
            d.line([(x, gy), (x + tw, gy)], fill=(70, 68, 80, 255))
            sheet.alpha_composite(im, (x, y))
            d.text((x + 6, y + 4), f"{TITLES.get(n, n)} · {ACT.get(labels[j], labels[j])}", font=font, fill=(255, 220, 120, 255))
        out.append(sheet.convert("RGB"))
    out[0].save(dst, save_all=True, append_images=out[1:], duration=33 * step, loop=0)
    print(dst, len(out), round(Path(dst).stat().st_size / 1e6, 1), "MB")


if __name__ == "__main__":
    dst, names = sys.argv[1], sys.argv[2:]
    for n in names:
        render(n)
    compose(names, dst)
