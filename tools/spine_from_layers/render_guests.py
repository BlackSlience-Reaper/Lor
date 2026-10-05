"""批量：按 guest_<名字>.json 生成分层骨骼到 build/guest_layers/<名字>/out，用游戏引擎渲染预览帧，
最后拼成 7×3 同步播放的总览 GIF（build/guests_layers.gif）。用法：render_guests.py [名字...]（缺省全部）"""
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

R = Path("/Users/iniad/LibraryOfRuina")
HERE = Path(__file__).resolve().parent
RENDER = Path.home() / ".claude/skills/sts2-spine-from-sprite/scripts/render.sh"
ORDER = ["eri", "yun", "finn", "philip", "salvador", "yuna", "oscar", "pamela", "pameli", "gin", "yang", "sayo",
         "mccullin", "naoki", "taein", "arnold", "consta", "mo", "meow", "mu_mu", "oink"]
PLAN = [["idle", True, 40], ["strike", False, 30], ["idle", True, 8], ["thrust", False, 30], ["idle", True, 8],
        ["slash", False, 30], ["idle", True, 8], ["hurt", False, 18], ["idle", True, 12], ["die", False, 40]]
ACT = {"idle": "待机", "strike": "打击", "thrust": "突刺", "slash": "斩击", "hurt": "受击", "die": "死亡"}
FEET_Y = 830  # 预览画布里脚底所在的行


def frames_dir(n):
    return Path(f"/tmp/spine_from_sprite_frames/layers_{n}")


def render(n):
    cfg_path = HERE / f"guest_{n}.json"
    cfg = json.loads(cfg_path.read_text())
    out = R / "build/guest_layers" / n
    subprocess.run(["python3", str(HERE / "spine_from_layers.py"), str(cfg_path), "--out", str(out / "out")], check=True)
    rj = {"name": cfg["name"], "out_dir": str(out / "out"), "frames_dir": str(frames_dir(n)), "fps": 30, "mix": 0.12,
          "viewport": [1200, 1000], "position": [600, FEET_Y - cfg["origin"][1]], "plan": PLAN}
    (out / "render.json").write_text(json.dumps(rj))
    r = subprocess.run(["bash", str(RENDER), str(out / "render.json")], capture_output=True, text=True)
    print(n, r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr[-300:], flush=True)


def compose(dst):
    names = json.loads((R / "LibraryOfRuina/localization/zhs/monsters.json").read_text(encoding="utf-8"))
    font = ImageFont.truetype("/System/Library/Fonts/Hiragino Sans GB.ttc", 15)
    big = ImageFont.truetype("/System/Library/Fonts/Hiragino Sans GB.ttc", 22)
    crop = (60, 60, 1140, 880)
    tw, th = 216, 164
    labels = (frames_dir(ORDER[0]) / "labels.txt").read_text().splitlines()
    total = min(len((frames_dir(n) / "labels.txt").read_text().splitlines()) for n in ORDER)
    out = []
    for i in range(total):
        sheet = Image.new("RGBA", (7 * tw, 3 * th + 34), (36, 34, 42, 255))
        d = ImageDraw.Draw(sheet)
        d.text((8, 4), f"来宾 21 人（原版分层）· {ACT.get(labels[i], labels[i])}", font=big, fill=(240, 240, 240, 255))
        for k, n in enumerate(ORDER):
            im = Image.open(frames_dir(n) / f"f_{i:03d}.png").convert("RGBA").crop(crop).resize((tw, th), Image.LANCZOS)
            x, y = (k % 7) * tw, 34 + (k // 7) * th
            d.line([(x, y + round((FEET_Y - crop[1]) * th / (crop[3] - crop[1]))), (x + tw, y + round((FEET_Y - crop[1]) * th / (crop[3] - crop[1])))],
                   fill=(70, 68, 80, 255))
            sheet.alpha_composite(im, (x, y))
            key = {"mu_mu": "MU_MU"}.get(n, n.upper())
            d.text((x + 4, y + 2), names.get(f"{key}.name", n), font=font, fill=(255, 220, 120, 255))
        out.append(sheet.convert("RGB"))
    out[0].save(dst, save_all=True, append_images=out[1:], duration=33, loop=0)
    print(dst, len(out), round(dst.stat().st_size / 1e6, 1), "MB")


if __name__ == "__main__":
    todo = sys.argv[1:] or ORDER
    for n in todo:
        render(n)
    compose(R / "build/guests_layers.gif")
