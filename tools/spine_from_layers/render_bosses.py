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
TITLES = {"lost_paradise": "失乐园", "false_throne": "伪王座", "twilight": "薄暝", "kali": "卡莉", "red_mist": "卡莉·红雾", "blind_rage": "盲眼怒火", "gold_rush_human": "闪金冲锋·人形", "gold_rush_king": "闪金冲锋·王形", "love_hatred_human": "以爱与憎之名·人形", "love_hatred_snake": "以爱与憎之名·蛇形", "tear_edge": "泪锋之剑", "nihil": "虚无缥缈", "nihil_despair": "虚无缥缈·绝望", "nihil_greed": "虚无缥缈·贪婪", "nihil_hatred": "虚无缥缈·憎恨", "nihil_wrath": "虚无缥缈·愤怒", "nihil_love": "爱之魔法少女", "nihil_justice": "正义魔法少女", "nihil_happiness": "幸福魔法少女", "nihil_courage": "勇气魔法少女", "black_swan": "黑天鹅", "bloodlust": "血欲", "laetitia": "Laetitia", "red_eyes": "赤瞳", "todays_expression": "此刻的神情", "scarlet_scar": "猩红创痕", "cobalt_scar": "郁蓝创痕", "cobalt_big_wolf": "郁蓝创痕·大坏狼", "cobalt_shadow": "郁蓝创痕·失去一切的狼", "dipsia": "渴血症", "mimicry_1": "拟态·一", "mimicry_2": "拟态·二", "mimicry_3": "拟态·三", "smiling_face": "笑靥", "emerald_bough": "翠枝", "fluttering": "翅振", "forgotten": "忘却", "wasp": "蜂后", "dacapo": "Da Capo", "little_galaxy": "我们的小小银河", "nostalgic_scent": "余香", "pleasure": "欢愉", "beyond_fragment": "彼方的碎片", "chord": "和弦", "grinder_mk4": "研削机Mk4", "magic_bullet": "魔弹", "regret": "悔恨", "solemn_mourning": "庄严哀悼"}
ACT = {"idle": "待机", "attack": "攻击", "attack_fire": "远程", "attack_strike": "打击", "attack_slash": "斩击",
       "attack_thrust": "突刺", "attack_right": "右击", "attack_left": "左击", "hurt": "受击", "guard": "防御",
       "parry": "格挡", "dodge": "闪避", "ego": "E.G.O", "ego_s1": "E.G.O 1", "ego_s2": "E.G.O 2", "ego_s3": "E.G.O 3",
       "special": "特殊", "die": "死亡", "ranged": "远程", "blunt": "打击", "pierce": "突刺", "slash": "斩击", "attack2": "攻击 2", "strike": "打击", "hunger": "饥饿连击", "cast": "施法", "shot_1": "射击 1", "shot_2": "射击 2", "shot_3": "射击 3", "s1": "技能 1", "s2": "技能 2", "howl": "嚎叫", "fire": "远程", "group_break": "群体破坏", "group_attack": "群体攻击", "evade": "闪避", "thrust": "突刺", "hello": "你好", "goodbye": "再见", "scream": "尖叫", "vomit": "呕吐", "slash_one": "斩击 1", "slash_two": "斩击 2", "persistence": "执着", "obsession": "痴迷", "desire_burst": "欲望爆发", "unbearable": "难以承受", "super_gift": "超级礼物", "flickering_eyes": "闪烁之眼", "unknown": "未知", "screech": "尖啸", "angry": "愤怒", "wavering_feelings": "动摇的感情", "special_s1": "特殊 1", "special_s2": "特殊 2", "special_s3": "特殊 3", "special_attack": "群体攻击", "intro": "起手", "stunned": "眩晕", "wake": "苏醒", "overflowing_light": "满溢之光", "area": "范围攻击", "rage": "暴怒", "polymorph": "变形", "forest_light": "林中之光", "brilliant_eyes": "炫目之眼", "punishment": "惩戒", "punishment_followup": "惩戒追击", "judgement": "审判", "peace": "和平", "blunt": "打击", "move": "移动", "blood_mist": "血雾", "field_of_corpses": "尸山"}


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
    crop = tuple(int(v) for v in os.environ.get("CROP", "100,0,1500,1000").split(","))  # 高个子（光环、头冠）不被切掉；特效宽的放大取景
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
