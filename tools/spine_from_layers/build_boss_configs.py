"""给有多种触发的 Boss 生成 spine_from_layers 配置（boss_<名字>.json）。

和来宾不同，Boss 的触发各有一张原图（远程、打击、格挡、E.G.O 技能……），这里每个触发一段动画：
- attack：先在待机姿势里蓄力（身体后仰、武器抡起），0.15 秒换成原版对应姿势，小幅前送，停 hold 秒后换回；
- hurt：立即换受伤姿势，后退、泛红，停 hold 秒后换回；
- guard：立即换防御姿势，小幅后坐，停 hold 秒后换回；
- skill：0.1 秒换技能姿势，身体略微拔高，停 hold 秒后换回。
停留时长照模组原来逐帧外观的 Swap 时长。骨头规则同来宾（build_guest_configs.rig_motion）；武器转轴手填。
用法：build_boss_configs.py <输出目录> [名字...]
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from build_guest_configs import rig_motion  # noqa: E402

LAYERS = Path.home() / ".local/share/LibraryOfRuina-layers"

# origin：模组待机图底边中点在原版坐标里的位置（type_tints.py 算出）
BOSSES = {
    "chord": {
        "prefix": "SingingMachine_", "origin": [28.5, 11.0],
        "weapons": [{"layers": ["body_10", "body_11", "body_12"], "pivot": [40, 215], "raise": 1}],
        "anims": {"attack_fire": ("attack", "Fire", 0.42), "attack_strike": ("attack", "Hit", 0.42),
                  "hurt": ("hurt", "Damaged", 0.40), "guard": ("guard", "Guard", 0.40),
                  "ego_s1": ("skill", "H_S1", 1.0), "ego_s2": ("skill", "Z_S2", 1.0)},
    },
    "grinder_mk4": {
        "prefix": "Helper_", "origin": [14.0, -11.0], "weapons": [],
        "anims": {"attack_slash": ("attack", "Slash", 0.5), "attack_thrust": ("attack", "Penetrate", 0.5),
                  "hurt": ("hurt", "Damaged", 0.28), "dodge": ("guard", "Evade", 0.6),
                  "ego_s1": ("skill", "Standing_Special", 0.5), "ego_s2": ("skill", "Hit_S1", 0.5),
                  "ego_s3": ("skill", "Hit_S2", 0.7)},
    },
    "magic_bullet": {
        "prefix": "ScorchedGirl_", "origin": [-140.0, -48.0],
        "weapons": [{"layers": ["body_8", "body_9"], "pivot": [-110, 180], "raise": -1}],
        "anims": {"attack": ("attack", "Fire", 0.7), "hurt": ("hurt", "Damaged", 0.40),
                  "special": ("skill", "Breakded_Special", 3.0)},
    },
    "regret": {
        "prefix": "Murderer_", "origin": [-169.0, -34.0],
        "weapons": [{"layers": ["body_3"], "pivot": [-55, 105], "raise": -1}],
        "anims": {"attack_right": ("attack", "HitR_Penetrate", 0.5), "attack_left": ("attack", "HitL_S1", 0.5),
                  "attack_slash": ("attack", "Slash", 0.5), "hurt": ("hurt", "Damaged", 0.28),
                  "parry": ("guard", "Guard", 0.6), "ego": ("skill", "Slash_S3", 1.1)},
    },
    "solemn_mourning": {
        "prefix": "Angela_Butterfly_", "origin": [-4.5, 7.0],
        "weapons": [{"layers": ["body_2"], "pivot": [-75, 195], "raise": -1},
                    {"layers": ["body_6"], "pivot": [95, 265], "raise": 1}],
        "anims": {"attack": ("attack", "F", 0.42), "hurt": ("hurt", "Damaged", 0.40),
                  "guard": ("guard", "Guard", 0.40), "ego_s1": ("skill", "S1", 1.0), "ego_s2": ("skill", "S2", 1.0)},
    },
}


def lag(pose, t0):
    return {f"{pose}.head": {"rotate": [[t0, -5], [t0 + 0.17, 4, "out"], [t0 + 0.4, 0]]},
            f"{pose}.hair": {"rotate": [[t0, -4], [t0 + 0.21, 5, "out"], [t0 + 0.45, 0]]}}


def weapon_keys(weapons, base, t_on, t_off, end, amount):
    keys = {}
    for i, w in enumerate(weapons):
        bone = f"{base}.weapon" + ("" if i == 0 else str(i + 1))
        a = w["raise"] * amount
        keys[bone] = {"rotate": [[0, 0], [t_on, a, "out"], [t_off, a], [end, 0]]}
    return keys


def animations(spec):
    D = "Default"
    weapons = spec["weapons"]
    idle_osc = {f"{D}.body": {"sy": [0.008, 1, 0.0]}, f"{D}.head": {"rotate": [1.8, 1, 0.15]},
                f"{D}.hair": {"rotate": [1.2, 1, 0.4]}}
    for i, _ in enumerate(weapons):
        idle_osc[f"{D}.weapon" + ("" if i == 0 else str(i + 1))] = {"rotate": [1.2, 1, 0.3 + 0.25 * i]}
    out = {"idle": {"duration": 2.4, "show": [[0, D, 0]], "osc": idle_osc}}
    for name, (kind, pose, hold) in spec["anims"].items():
        if kind == "attack":
            on, off = 0.15, 0.15 + hold
            end = off + 0.3
            keys = {f"{D}.body": {"rotate": [[0, 0], [on, -4, "out"], [off, -4], [end, 0]]},
                    f"{D}.head": {"rotate": [[0, 0], [on, 3, "out"], [off, 0], [end, 0]]},
                    f"{pose}.body": {"x": [[on, 12], [on + 0.15, -12, "out"], [on + 0.4, 0]],
                                     "rotate": [[on, -3], [on + 0.15, 2, "out"], [on + 0.35, 0]]},
                    **weapon_keys(weapons, D, on, off, end, 18), **lag(pose, on)}
            out[name] = {"duration": round(end, 3), "show": [[0, D, 0], [on, pose, 0], [off, D, 0]], "keys": keys}
        elif kind == "hurt":
            end = hold + 0.25
            keys = {"move": {"x": [[0, 0], [0.1, 20, "out"], [end - 0.05, 0]]},
                    f"{pose}.body": {"rotate": [[0, 0], [0.08, -6, "out"], [hold, 0]]},
                    f"{pose}.head": {"rotate": [[0, 0], [0.1, -8, "out"], [hold + 0.05, 0]]},
                    f"{pose}.hair": {"rotate": [[0, 0], [0.14, -10, "out"], [hold + 0.1, 0]]}}
            out[name] = {"duration": round(end, 3), "show": [[0, pose, 0], [hold, D, 0]],
                         "flash": [0.0, 0.22, [1.0, 0.6, 0.6]], "keys": keys}
        elif kind == "guard":
            end = hold + 0.25
            keys = {f"{pose}.body": {"x": [[0, 0], [0.08, 8, "out"], [0.35, 0]]}, **lag(pose, 0.0)}
            out[name] = {"duration": round(end, 3), "show": [[0, pose, 0], [hold, D, 0]], "keys": keys}
        else:
            on = 0.1
            off = on + hold
            end = off + 0.3
            keys = {f"{D}.body": {"sy": [[0, 1], [on, 0.97, "out"], [off, 0.97], [end, 1]]},
                    f"{pose}.body": {"y": [[on, 0], [on + 0.2, 6, "out"], [off, 0]]},
                    **lag(pose, on)}
            out[name] = {"duration": round(end, 3), "show": [[0, D, 0], [on, pose, 0], [off, D, 0]], "keys": keys}
    out["die"] = {"duration": 1.2, "show": [[0, D, 0]],
                  "keys": {f"{D}.body": {"rotate": [[0, 0], [0.9, 3, "out"], [1.2, 3]],
                                         "sy": [[0, 1], [0.9, 0.96, "out"], [1.2, 0.96]]},
                           f"{D}.head": {"rotate": [[0, 0], [0.8, 16, "out"], [1.2, 16]]},
                           f"{D}.hair": {"rotate": [[0, 0], [0.9, 8, "out"], [1.2, 8]]},
                           **{f"{D}.weapon" + ("" if i == 0 else str(i + 1)):
                              {"rotate": [[0, 0], [0.7, -w["raise"] * 5, "out"], [1.2, -w["raise"] * 5]]}
                              for i, w in enumerate(weapons)}}}
    return out


def main():
    out_dir = Path(sys.argv[1])
    only = set(sys.argv[2:])
    for name, spec in BOSSES.items():
        if only and name not in only:
            continue
        info = json.loads((LAYERS / name / "layers.json").read_text())
        poses = ["Default"] + sorted({p for _, p, _ in spec["anims"].values()} - {"Default"})
        motions = {}
        for pose in poses:
            src = spec["prefix"] + pose
            bones, assign, _ = rig_motion(LAYERS / name, src, info[src]["layers"])
            if pose == "Default":
                for i, w in enumerate(spec["weapons"]):
                    role = "weapon" + ("" if i == 0 else str(i + 1))
                    bones[role] = {"parent": "body", "at": w["pivot"]}
                    for l in w["layers"]:
                        assign[l] = role
            motions[pose] = {"source": src, "bones": bones, "assign": assign}
        cfg = {"name": f"boss_{name}", "layers_dir": f"~/.local/share/LibraryOfRuina-layers/{name}",
               "origin": spec["origin"], "skin_tint": [1.0, 1.0, 1.0], "fps": 30, "setup_motion": "Default",
               "motions": motions, "animations": animations(spec)}
        (out_dir / f"boss_{name}.json").write_text(json.dumps(cfg, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
        print(name, list(cfg["animations"]))


if __name__ == "__main__":
    main()
