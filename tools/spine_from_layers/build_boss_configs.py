"""给有多种触发的 Boss 生成 spine_from_layers 配置（boss_<名字>.json）。

和来宾不同，Boss 的触发各有一张原图（远程、打击、格挡、E.G.O 技能……），这里每个触发一段动画：
- attack：先在待机姿势里蓄力（身体后仰、武器抡起），0.15 秒换成原版对应姿势，小幅前送，停 hold 秒后换回；
- hurt：立即换受伤姿势，后退、泛红，停 hold 秒后换回；
- guard：立即换防御姿势，小幅后坐，停 hold 秒后换回；
- skill：0.1 秒换技能姿势，身体略微拔高，停 hold 秒后换回。
停留时长按动作类型统一（HOLD；seq 每个姿势 SEQ_STEP 秒），个别要和战斗里的等待对齐的写在 holds。骨头规则同来宾（build_guest_configs.rig_motion）；武器转轴手填。
用法：build_boss_configs.py <输出目录> [名字...]
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from build_guest_configs import rig_motion  # noqa: E402

LAYERS = Path.home() / ".local/share/LibraryOfRuina-layers"

# 换上的姿势停留多久。原来逐帧外观各自写的时长从 0.12 秒到 2.2 秒不等，换成骨骼动作后节奏差得很明显，
# 所以按类型统一；guard 也用于闪避、招架。
HOLD = {"attack": 0.6, "hurt": 0.4, "guard": 0.55, "skill": 0.8}
SEQ_STEP = 0.4

# origin：模组待机图底边中点在原版坐标里的位置（type_tints.py 算出）
BOSSES = {
    "chord": {
        "prefix": "SingingMachine_", "origin": [28.5, 11.0],
        "weapons": [{"layers": ["body_10", "body_11", "body_12"], "pivot": [40, 215], "raise": 1}],
        "anims": {"attack_fire": ("attack", "Fire"), "attack_strike": ("attack", "Hit"),
                  "hurt": ("hurt", "Damaged"), "guard": ("guard", "Guard"),
                  "ego_s1": ("skill", "H_S1"), "ego_s2": ("skill", "Z_S2")},
    },
    "grinder_mk4": {
        "prefix": "Helper_", "origin": [14.0, -11.0], "weapons": [],
        "anims": {"attack_slash": ("attack", "Slash"), "attack_thrust": ("attack", "Penetrate"),
                  "hurt": ("hurt", "Damaged"), "dodge": ("guard", "Evade"),
                  "ego_s1": ("skill", "Standing_Special"), "ego_s2": ("skill", "Hit_S1"),
                  "ego_s3": ("skill", "Hit_S2")},
    },
    "magic_bullet": {
        "prefix": "ScorchedGirl_", "origin": [-140.0, -48.0],
        "weapons": [{"layers": ["body_8", "body_9"], "pivot": [-110, 180], "raise": -1}],
        "anims": {"attack": ("attack", "Fire"), "hurt": ("hurt", "Damaged"),
                  "special": ("skill", "Breakded_Special")},
        "holds": {"special": 3.0},  # 战斗里 TriggerAnim("Special", 3.0f) 会等满 3 秒
    },
    "regret": {
        "prefix": "Murderer_", "origin": [-169.0, -34.0],
        "weapons": [{"layers": ["body_3"], "pivot": [-55, 105], "raise": -1}],
        "anims": {"attack_right": ("attack", "HitR_Penetrate"), "attack_left": ("attack", "HitL_S1"),
                  "attack_slash": ("attack", "Slash"), "hurt": ("hurt", "Damaged"),
                  "parry": ("guard", "Guard"), "ego": ("skill", "Slash_S3")},
    },
    # 历史层（第 4 项是位移方式，见 move_keys）
    "emerald_bough": {
        "prefix": "SnowWhite_", "origin": [-3.0, -12.0], "weapons": [],
        "anims": {"attack": ("attack", "Fire", "recoil"), "guard": ("guard", "Guard", "brace"),
                  "ego": ("skill", "S1", "rise"), "hurt": ("hurt", "Damaged")},
    },
    "fluttering": {
        # 模组翅振的贴图是原版合成图放大 1.2 倍（外观缩放 0.4756 抵消），骨架整体同倍放大才对得上
        "prefix": "Fairy", "base": "_Default", "origin": [-63.5, -3.8], "root_scale": 1.2,
        "weapons": [{"layers": ["body"], "pivot": [-63, 245], "raise": 0, "idle": {"sx": [0.06, 4, 0.0]}}],
        "anims": {"strike": ("attack", "_Hit", "dash"), "slash": ("attack", "_Slash", "dash"),
                  "hunger": ("seq", ["SpecialX_S2", "_S3", "_S4"], "dash"),
                  "guard": ("guard", "_Default", "brace"), "hurt": ("hurt", "_Damaged")},
    },
    "forgotten": {
        "prefix": "Teddy_", "origin": [-38.0, -93.0],
        "weapons": [{"layers": ["body_3"], "pivot": [-60, 250], "raise": 1},
                    {"layers": ["body_2"], "pivot": [55, 250], "raise": 1}],
        "anims": {"strike": ("attack", "Hit", "dash"), "slash": ("attack", "Slash", "step"),
                  "special": ("skill", "S1", "step"), "hurt": ("hurt", "Damaged")},
    },
    "wasp": {
        "prefix": "Angela_Queenbee_", "origin": [-126.5, -185.0],
        "weapons": [{"layers": ["body_6"], "pivot": [-60, 175], "raise": -1},
                    {"layers": ["body"], "pivot": [-30, 340], "raise": 0, "idle": {"sx": [0.06, 4, 0.0]}}],
        "anims": {"strike": ("attack", "Hit", "dash"), "pierce": ("attack", "Penetrate", "dash"),
                  "cast": ("guard", "Evade", "hop"), "hurt": ("hurt", "Damaged")},
    },
    # 语言层。原版 E.G.O 动作名不带前缀；郁蓝创痕和拟态按形态各一副骨架（layers 指向同一人物的不同预制体）
    "scarlet_scar": {
        "prefix": "", "origin": [-99.5, -121.0], "weapons": [],
        "anims": {"slash": ("attack", "Slash", "step"), "shot_1": ("attack", "S1", "recoil"),
                  "shot_2": ("attack", "S2", "recoil"), "shot_3": ("attack", "S3", "recoil"),
                  "hurt": ("hurt", "Damaged")},
    },
    "cobalt_scar": {
        "layers": "cobalt_normal", "prefix": "", "origin": [48.5, -16.0], "weapons": [],
        "anims": {"strike": ("attack", "Hit", "dash"), "slash": ("attack", "Slash", "dash"),
                  "hurt": ("hurt", "Damaged")},
    },
    "cobalt_big_wolf": {
        "layers": "cobalt_polymorph", "prefix": "", "origin": [76.0, -16.0], "weapons": [],
        "anims": {"strike": ("attack", "Hit", "dash"), "slash": ("attack", "Slash", "dash"),
                  "guard": ("guard", "Guard", "brace"), "s1": ("attack", "S1", "dash"),
                  "s2": ("skill", "S2", "step"), "hurt": ("hurt", "Damaged")},
    },
    "cobalt_shadow": {
        "layers": "cobalt_stealth", "prefix": "", "origin": [122.0, -26.0], "weapons": [],
        "anims": {"attack": ("attack", "Penetrate", "dash"), "howl": ("skill", "Howl_S2", "rise"),
                  "hurt": ("hurt", "Damaged")},
    },
    "dipsia": {
        "prefix": "", "origin": [-29.0, -19.0], "weapons": [],
        "anims": {"fire": ("attack", "Fire", "recoil"), "strike": ("attack", "Hit", "dash"),
                  "slash": ("attack", "Slash", "step"), "group_break": ("skill", "S1", "step"),
                  "group_attack": ("attack", "S2", "dash"), "evade": ("guard", "Evade", "hop"),
                  "hurt": ("hurt", "Damaged")},
        "holds": {"group_break": 0.55},  # 战斗里按 GroupBreakSegmentSeconds 分段等待
    },
    "mimicry_1": {
        "layers": "mimicry_lv1", "prefix": "", "origin": [9.0, -35.0], "weapons": [],
        "anims": {"thrust": ("attack", "Penetrate", "dash"), "parry": ("guard", "Default", "brace"),
                  "hurt": ("hurt", "Damaged")},
    },
    "mimicry_2": {
        # 原版第二形态只有一张整图：受击、招架都用待机姿势，只靠整体位移和泛红
        "layers": "mimicry_lv2", "prefix": "", "origin": [-21.0, 4.0], "weapons": [],
        "anims": {"parry": ("guard", "Default", "brace"), "hurt": ("hurt", "Default")},
    },
    "mimicry_3": {
        "layers": "mimicry_lv3", "prefix": "", "origin": [-43.0, -263.0], "weapons": [],
        "anims": {"strike": ("attack", "Hit", "dash"), "thrust": ("attack", "Penetrate", "dash"),
                  "slash": ("attack", "Slash", "step"), "parry": ("guard", "Guard", "brace"),
                  "hello": ("skill", "Fire", "step"), "goodbye": ("skill", "GoodBye_S1", "dash"),
                  "hurt": ("hurt", "Damaged")},
        "holds": {"goodbye": 1.4},  # 再见的斩击特效按 1.4 秒排
    },
    "smiling_face": {
        "prefix": "", "origin": [-83.5, -306.0], "weapons": [],
        "anims": {"thrust": ("attack", "Penetrate", "step"), "slash": ("attack", "Slash", "step"),
                  "scream": ("skill", "Shout_S1", "rise"), "vomit": ("skill", "Vomit_S2", "step"),
                  "hurt": ("hurt", "Damaged")},
    },
    # 艺术层
    "dacapo": {
        "prefix": "Orchestra_", "origin": [-2.5, -136.0],
        "weapons": [{"layers": ["body_11"], "pivot": [45, 250], "raise": -1}],
        "anims": {"attack": ("attack", "Hit"), "hurt": ("hurt", "Damaged"),
                  "guard": ("guard", "Guard"), "special": ("skill", "Special")},
    },
    "little_galaxy": {
        "prefix": "GalaxyChild_", "origin": [-15.0, -81.0], "weapons": [],
        # 原版 S2、S4 的弹道是单独播放的特效，模组原图上叠着；extract_fx.py 抠出后作为额外层挂在姿势上
        "extra": {"S2": [{"name": "fx", "image": "fx/little_galaxy_s2.png", "center": [-469.0, 185.0]}],
                  "S4": [{"name": "fx", "image": "fx/little_galaxy_s4.png", "center": [-632.0, 438.0]}]},
        "anims": {"attack": ("seq", ["S4", "S3", "S2", "S1"]), "hurt": ("hurt", "Damaged")},
    },
    "nostalgic_scent": {
        "prefix": "Alriune_", "origin": [-24.0, -35.0],
        "weapons": [{"layers": ["body"], "pivot": [-50, 120], "raise": -1},
                    {"layers": ["body_3"], "pivot": [60, 115], "raise": 1}],
        "anims": {"ranged": ("attack", "Far"), "blunt": ("attack", "Hit"),
                  "pierce": ("attack", "Penetrate"), "hurt": ("hurt", "Damaged"),
                  "guard": ("guard", "Guard"), "ego_s1": ("seq", ["S1", "S2", "S3"]),
                  "ego_s2": ("seq", ["S2", "S3", "S1"]), "ego_s3": ("seq", ["S3", "S1", "S2"])},
    },
    "pleasure": {
        "prefix": "Porccubus_", "origin": [50.0, 6.0],
        "weapons": [{"layers": ["body"], "pivot": [40, 140], "raise": 1}],
        "anims": {"blunt": ("attack", "Hit"), "pierce": ("attack", "Penetrate"),
                  "slash": ("attack", "Slash"), "dodge": ("guard", "Penetrate"),
                  "ego_s1": ("skill", "Z_S1"), "ego_s2": ("skill", "Z_S2"),
                  "hurt": ("hurt", "Damaged")},
    },
    "beyond_fragment": {
        "prefix": "UniverseFragment_", "origin": [-173.0, -70.0],
        "weapons": [{"layers": ["body"], "pivot": [-10, 215], "raise": -1}],
        "anims": {"attack": ("attack", "ZUp_S1"), "attack2": ("attack", "ZDown_S2"),
                  "ego": ("skill", "S3"), "hurt": ("hurt", "Damaged")},
    },
    "solemn_mourning": {
        "prefix": "Angela_Butterfly_", "origin": [-4.5, 7.0],
        "weapons": [{"layers": ["body_2"], "pivot": [-75, 195], "raise": -1},
                    {"layers": ["body_6"], "pivot": [95, 265], "raise": 1}],
        "anims": {"attack": ("attack", "F"), "hurt": ("hurt", "Damaged"),
                  "guard": ("guard", "Guard"), "ego_s1": ("skill", "S1"), "ego_s2": ("skill", "S2")},
    },
}


def lag(pose, t0):
    return {f"{pose}.head": {"rotate": [[t0, -5], [t0 + 0.17, 4, "out"], [t0 + 0.4, 0]]},
            f"{pose}.hair": {"rotate": [[t0, -4], [t0 + 0.21, 5, "out"], [t0 + 0.45, 0]]}}


def weapon_keys(weapons, base, t_on, t_off, end, amount):
    keys = {}
    for i, w in enumerate(weapons):
        if not w.get("raise"):
            continue  # 翅膀之类只在待机时扇动，不参与蓄力
        bone = f"{base}.weapon" + ("" if i == 0 else str(i + 1))
        a = w["raise"] * amount
        keys[bone] = {"rotate": [[0, 0], [t_on, a, "out"], [t_off, a], [end, 0]]}
    return keys


# 按动作类型给整体（move 骨头，x 负值朝前）加位移：dash 冲上前、step 踏一步、recoil 射击后坐、hop 往后跳、
# brace 往后顶、rise 略微浮起。t0/t1 是换上姿势与换回的时刻。
def move_keys(style, t0, t1, end):
    if style == "dash":
        return {"move": {"x": [[0, 0], [t0, 0], [t0 + 0.15, -70, "out"], [t1, -70], [end, 0]]}}
    if style == "step":
        return {"move": {"x": [[0, 0], [t0, 0], [t0 + 0.12, -25, "out"], [t1, -25], [end, 0]]}}
    if style == "recoil":
        return {"move": {"x": [[0, 0], [t0, 0], [t0 + 0.08, 16, "out"], [t1, 0], [end, 0]]}}
    if style == "hop":
        return {"move": {"x": [[0, 0], [t0 + 0.15, 45, "out"], [t1, 45], [end, 0]],
                         "y": [[0, 0], [t0 + 0.08, 18, "out"], [t0 + 0.2, 0, "in"], [end, 0]]}}
    if style == "brace":
        return {"move": {"x": [[0, 0], [t0 + 0.08, 12, "out"], [t1, 6], [end, 0]]}}
    if style == "rise":
        return {"move": {"y": [[0, 0], [t0 + 0.25, 14, "out"], [t1, 14], [end, 0]]}}
    return {}


def animations(spec):
    D = spec.get("base", "Default")  # 待机姿势；原版动作名不统一时（翅振 FairySpecialX_S2）前缀写短、这里写全
    weapons = spec["weapons"]
    idle_osc = {f"{D}.body": {"sy": [0.008, 1, 0.0]}, f"{D}.head": {"rotate": [1.8, 1, 0.15]},
                f"{D}.hair": {"rotate": [1.2, 1, 0.4]}}
    for i, w in enumerate(weapons):
        idle_osc[f"{D}.weapon" + ("" if i == 0 else str(i + 1))] = w.get("idle", {"rotate": [1.2, 1, 0.3 + 0.25 * i]})
    out = {"idle": {"duration": 2.4, "show": [[0, D, 0]], "osc": idle_osc}}
    for name, (kind, pose, *opt) in spec["anims"].items():
        style = opt[0] if opt else None
        hold = spec.get("holds", {}).get(name) or (SEQ_STEP * len(pose) if kind == "seq" else HOLD[kind])
        if kind == "attack":
            on, off = 0.15, 0.15 + hold
            end = off + 0.3
            keys = {f"{D}.body": {"rotate": [[0, 0], [on, -4, "out"], [off, -4], [end, 0]]},
                    f"{D}.head": {"rotate": [[0, 0], [on, 3, "out"], [off, 0], [end, 0]]},
                    f"{pose}.body": {"x": [[on, 12], [on + 0.15, -12, "out"], [on + 0.4, 0]],
                                     "rotate": [[on, -3], [on + 0.15, 2, "out"], [on + 0.35, 0]]},
                    **weapon_keys(weapons, D, on, off, end, 18), **lag(pose, on)}
            keys.update(move_keys(style, on, off, end))
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
            keys.update(move_keys(style, 0.0, hold, end))
            out[name] = {"duration": round(end, 3), "show": [[0, pose, 0], [hold, D, 0]], "keys": keys}
        elif kind == "seq":
            # 多姿势连段：每个姿势停 SEQ_STEP 秒，依次瞬间切换；每次切换头和头发都甩一下
            poses = pose
            on = 0.1
            step = hold / len(poses)
            off = on + hold
            end = off + 0.3
            show = [[0, D, 0]] + [[round(on + i * step, 3), p, 0] for i, p in enumerate(poses)] + [[round(off, 3), D, 0]]
            keys = {f"{D}.body": {"rotate": [[0, 0], [on, -3, "out"], [off, -3], [end, 0]]}}
            for i, p in enumerate(poses):
                t0 = on + i * step
                keys[f"{p}.body"] = {"x": [[t0, 8], [t0 + min(0.15, step * 0.5), -6, "out"], [t0 + step, 0]]}
                keys.update(lag(p, t0))
            keys.update(move_keys(style, on, off, end))
            out[name] = {"duration": round(end, 3), "show": show, "keys": keys}
        else:
            on = 0.1
            off = on + hold
            end = off + 0.3
            keys = {f"{D}.body": {"sy": [[0, 1], [on, 0.97, "out"], [off, 0.97], [end, 1]]},
                    f"{pose}.body": {"y": [[on, 0], [on + 0.2, 6, "out"], [off, 0]]},
                    **lag(pose, on)}
            keys.update(move_keys(style, on, off, end))
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
        src_dir = spec.get("layers", name)
        info = json.loads((LAYERS / src_dir / "layers.json").read_text())
        used = set()
        for _, p, *_ in spec["anims"].values():
            used.update(p if isinstance(p, list) else [p])
        base = spec.get("base", "Default")
        poses = [base] + sorted(used - {base})
        motions = {}
        for pose in poses:
            src = spec["prefix"] + pose
            bones, assign, _ = rig_motion(LAYERS / src_dir, src, info[src]["layers"])
            extra = spec.get("extra", {}).get(pose, [])
            for ex in extra:
                assign[ex["name"]] = "body"
            if pose == base:
                for i, w in enumerate(spec["weapons"]):
                    role = "weapon" + ("" if i == 0 else str(i + 1))
                    bones[role] = {"parent": "body", "at": w["pivot"]}
                    for l in w["layers"]:
                        assign[l] = role
            motions[pose] = {"source": src, "bones": bones, "assign": assign, **({"extra": extra} if extra else {})}
        cfg = {"name": f"boss_{name}", "layers_dir": f"~/.local/share/LibraryOfRuina-layers/{src_dir}", "texture_scale": 0.6,
               **({"root_scale": spec["root_scale"]} if "root_scale" in spec else {}),
               "origin": spec["origin"], "skin_tint": [1.0, 1.0, 1.0], "fps": 30, "setup_motion": base,
               "motions": motions, "animations": animations(spec)}
        (out_dir / f"boss_{name}.json").write_text(json.dumps(cfg, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
        print(name, list(cfg["animations"]))


if __name__ == "__main__":
    main()
