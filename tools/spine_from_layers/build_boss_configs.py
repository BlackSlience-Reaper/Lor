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
    # 文学层。原来是场景动画：多段招式照 scenes/creature_visuals/*_animations.tres 的换图时刻（scene_timelines.py 读出）
    # 做成 timeline，hits 是动画契约（LiteratureFloor*AnimationContract）里的命中时刻；单张图的动作按统一时长。
    # 模组图上叠的特效（Laetitia 的心、赤瞳的刀光、此刻的神情的血肉团）用 extract_fx.py 抠成额外层。
    "black_swan": {
        "prefix": "EGO_BlackSwan_", "origin": [21.5, -27.0], "weapons": [],
        "anims": {"slash_one": ("attack", "Slash", "step"), "slash_two": ("attack", "Slash_S2", "step"),
                  "pierce": ("attack", "Penetrate", "dash"), "guard": ("seq", ["Guard", "Guard_S1"], "brace"),
                  "special": ("skill", "Special", "rise"), "hurt": ("hurt", "Damaged")},
    },
    "bloodlust": {
        "prefix": "EGO_RedShoes_", "origin": [-55.5, -124.0], "weapons": [],
        "anims": {"persistence": ("timeline", {"frames": [[0, "Hit"], [0.9, "Slash"]], "length": 1.8,
                                               "hits": [0.5, 1.3]}, "dash"),
                  "obsession": ("timeline", {"frames": [[0, "Slash"]], "length": 1.3, "hits": [0.64]}, "step"),
                  "desire_burst": ("timeline", {"frames": [[0, "Hit"], [0.72, "Slash"], [1.44, "Hit"], [1.86, "Evade"]],
                                                "length": 2.3, "hits": [0.4, 1.1, 1.8]}, "dash"),
                  "unbearable": ("timeline", {"frames": [[0, "HS"], [0.6, "SpecialCard_JS5"], [1.2, "HS"],
                                                         [1.8, "SpecialCard_JS5"]],
                                              "length": 4.1, "hits": [0.5, 1.1, 1.7, 2.3, 3.3]}, "dash"),
                  "cast": ("guard", "Evade", "hop"), "hurt": ("hurt", "Damaged")},
    },
    "laetitia": {
        "prefix": "EGO_Latitia_", "origin": [-56.0, 8.0], "weapons": [],
        "extra": {"S1": [{"name": "fx", "image": "fx/laetitia_super_s1.png", "center": [-56.5, 365.0]}],
                  "S2": [{"name": "fx", "image": "fx/laetitia_super_s2.png", "center": [-151.0, 624.5]}],
                  "S3": [{"name": "fx", "image": "fx/laetitia_super_s3.png", "center": [-50.5, 525.0]}]},
        "anims": {"attack": ("attack", "Fire", "step"), "cast": ("guard", "Guard", "brace"),
                  "super_gift": ("timeline", {"frames": [[0, "S1"], [0.933, "S2"], [1.8, "S3"]], "length": 3.3}, "rise"),
                  "hurt": ("hurt", "Damaged")},
    },
    "red_eyes": {
        "prefix": "EGO_SpiderBud_", "origin": [-297.5, -201.0], "weapons": [],
        "extra": {"S1": [{"name": "fx", "image": "fx/red_eyes_s1.png", "center": [-182.0, 38.0]}],
                  "Slash": [{"name": "fx", "image": "fx/red_eyes_slash.png", "center": [-578.0, 285.0]}],
                  "Hit": [{"name": "fx", "image": "fx/red_eyes_strike.png", "center": [65.5, 396.0]}]},
        "anims": {"flickering_eyes": ("skill", "Special", "rise"), "unknown": ("skill", "Special", "step"),
                  "screech": ("timeline", {"frames": [[0, "Special"], [0.533, "Hit"], [1.1, "Slash"], [2.067, "S1"]],
                                           "length": 3.0, "hits": [0.5, 1.1, 2.1]}, "dash"),
                  "hurt": ("hurt", "Damaged")},
    },
    "todays_expression": {
        "prefix": "Ego_ShyLookToday_", "origin": [3.5, -1.0], "weapons": [],
        "extra": {"Guard": [{"name": "fx", "image": "fx/todays_expression_guard.png", "center": [-280.5, 254.5]}],
                  "G_S1": [{"name": "fx", "image": "fx/todays_expression_s1.png", "center": [-369.5, 381.5]}],
                  "Atk_S2": [{"name": "fx", "image": "fx/todays_expression_s2.png", "center": [-362.0, 368.5]}],
                  "Penetrate": [{"name": "fx", "image": "fx/todays_expression_thrust.png", "center": [-562.0, 210.5]}]},
        "anims": {"attack": ("timeline", {"frames": [[0, "Penetrate"]], "length": 1.3, "hits": [0.64]}, "step"),
                  "angry": ("timeline", {"frames": [[0, "Penetrate"]], "length": 2.1, "hits": [0.44, 1.04, 1.64]}, "step"),
                  "wavering_feelings": ("timeline", {"frames": [[0, "G_S1"], [0.9, "Atk_S2"]], "length": 2.5,
                                                     "hits": [1.44]}, "step"),
                  "guard": ("guard", "Guard", "brace"), "hurt": ("hurt", "Damaged")},
    },
    # 自然层。原来也是场景动画，但每个动作只有一张图，按统一时长。场景按形态换动画库（闪金冲锋人形/王形与蓄力、
    # 以爱与憎之名人形/蛇形/特殊待机、泪锋之剑五个形态、虚无缥缈五个阶段），每个库一副骨架，待机姿势就是该库的 Idle 图。
    # 蛇形模组图是原版实际大小的 1.25 倍（root_scale）；爱之魔法少女原版每单位 50 像素，模组图只有实际的一半。魔法少女和蛇形在原版只有整图。
    "blind_rage": {
        "prefix": "EGO_Wrath_", "origin": [-145.5, -303.0], "weapons": [],
        "anims": {"attack_strike": ("attack", "Hit", "dash"), "attack_thrust": ("attack", "Penetrate", "dash"),
                  "attack_slash": ("attack", "Slash", "step"), "special_s1": ("attack", "S1", "dash"),
                  "special_s2": ("attack", "S2", "step"), "special_s3": ("attack", "S3", "dash"),
                  "hurt": ("hurt", "Damaged")},
    },
    "gold_rush_human": {
        "layers": "gold_rush_human", "prefix": "Greed_", "origin": [57.0, 8.0], "weapons": [],
        "anims": {"attack": ("attack", "Hit", "dash"), "guard": ("guard", "Guard", "brace"),
                  "intro": ("skill", "Special", "rise"), "special_attack": ("attack", "S1", "dash"),
                  "hurt": ("hurt", "Damaged")},
    },
    "gold_rush_human_charging": {
        "layers": "gold_rush_human", "prefix": "Greed_", "base": "Special", "origin": [74.0, 4.0], "weapons": [],
        "anims": {"hurt": ("hurt", "Special")},
    },
    "gold_rush_king": {
        "layers": "gold_rush_king", "prefix": "Greed_", "origin": [53.0, 6.0], "weapons": [],
        "anims": {"attack": ("attack", "Hit", "dash"), "guard": ("guard", "Guard", "brace"),
                  "intro": ("skill", "Special", "rise"), "special_attack": ("attack", "S1", "dash"),
                  "hurt": ("hurt", "Damaged")},
    },
    "gold_rush_king_charging": {
        "layers": "gold_rush_king", "prefix": "Greed_", "base": "Special", "origin": [74.0, 4.0], "weapons": [],
        "anims": {"hurt": ("hurt", "Special")},
    },
    "love_hatred_human": {
        "prefix": "HatredHuman_", "origin": [-13.0, -165.0], "weapons": [],
        "anims": {"strike": ("attack", "Hit", "dash"), "fire": ("attack", "Fire", "recoil"),
                  "guard": ("guard", "Guard", "brace"), "hurt": ("hurt", "Damaged")},
    },
    "love_hatred_special": {
        "layers": "love_hatred_human", "prefix": "HatredHuman_", "base": "Special", "origin": [-20.5, -68.0], "weapons": [],
        "anims": {"strike": ("attack", "Hit", "dash"), "fire": ("attack", "Fire", "recoil"),
                  "guard": ("guard", "Guard", "brace"), "hurt": ("hurt", "Damaged")},
    },
    "love_hatred_snake": {
        "prefix": "HatredSnake_", "origin": [17.8, -51.8], "root_scale": 1.25, "weapons": [],
        "anims": {"strike": ("attack", "Penetrate", "dash"), "fire": ("attack", "Fire", "recoil"),
                  "guard": ("guard", "Guard", "brace"), "hurt": ("hurt", "Damaged")},
    },
    "tear_edge": {
        "prefix": "Despair_", "origin": [92.5, 2.0], "weapons": [],
        "anims": {"attack": ("attack", "Special", "dash"), "guard": ("guard", "Damaged", "brace"),
                  "hurt": ("hurt", "Damaged")},
    },
    "tear_edge_despair": {
        "layers": "tear_edge", "prefix": "Despair_", "base": "Special", "origin": [-221.5, 0.0], "weapons": [],
        "anims": {"attack": ("attack", "Special", "dash"), "guard": ("guard", "Damaged", "brace"),
                  "hurt": ("hurt", "Damaged")},
    },
    # 被剑刺穿的三个形态：场景里所有动作都停在同一张图，骨架只做受击晃动
    "tear_edge_stabbed1": {"layers": "tear_edge", "prefix": "Despair_", "base": "S1", "origin": [-10.5, -71.0],
                           "weapons": [], "anims": {"hurt": ("hurt", "S1")}},
    "tear_edge_stabbed2": {"layers": "tear_edge", "prefix": "Despair_", "base": "S2", "origin": [12.5, -98.0],
                           "weapons": [], "anims": {"hurt": ("hurt", "S2")}},
    "tear_edge_stabbed3": {"layers": "tear_edge", "prefix": "Despair_", "base": "S3", "origin": [62.5, -97.0],
                           "weapons": [], "anims": {"hurt": ("hurt", "S3")}},
    "nihil": {
        "prefix": "Nihil_", "origin": [-32.0, 5.0], "weapons": [],
        "anims": {"attack": ("attack", "Special", "dash"), "special": ("skill", "Special", "rise"),
                  "guard": ("guard", "Guard", "brace"), "evade": ("guard", "Evade", "hop"),
                  "hurt": ("hurt", "Damaged"), "stunned": ("hurt", "Damaged")},
        "holds": {"stunned": 2.0},
    },
    "nihil_despair": {
        "prefix": "Despair_", "origin": [-32.0, 5.0], "weapons": [],
        "extra": {"Fire": [{"name": "fx", "image": "fx/nihil_despair_fire.png", "center": [-348.5, 476.0]}]},
        "anims": {"attack": ("attack", "Fire", "recoil"), "special": ("skill", "Fire", "recoil"),
                  "guard": ("guard", "Guard", "brace"), "evade": ("guard", "Evade", "hop"),
                  "hurt": ("hurt", "Damaged"), "stunned": ("hurt", "Damaged")},
        "holds": {"stunned": 2.0},
    },
    "nihil_greed": {
        "prefix": "GreedPhase_", "origin": [-32.0, 5.0], "weapons": [],
        "anims": {"slash": ("attack", "Slash", "step"), "strike": ("attack", "Hit", "dash"),
                  "special": ("skill", "S1", "dash"), "guard": ("guard", "Guard", "brace"),
                  "evade": ("guard", "Evade", "hop"), "hurt": ("hurt", "Damaged"), "stunned": ("hurt", "Damaged")},
        "holds": {"stunned": 2.0},
    },
    "nihil_hatred": {
        "prefix": "Hatred_", "origin": [-32.0, 5.0], "weapons": [],
        "anims": {"slash": ("attack", "Slash", "step"), "fire": ("attack", "Fire", "recoil"),
                  "special": ("skill", "Special", "rise"), "guard": ("guard", "Guard", "brace"),
                  "evade": ("guard", "Evade", "hop"), "hurt": ("hurt", "Damaged"), "stunned": ("hurt", "Damaged")},
        "holds": {"stunned": 2.0},
    },
    "nihil_wrath": {
        "prefix": "Wrath_", "origin": [-32.0, 5.0], "weapons": [],
        "anims": {"slash": ("attack", "Slash", "step"), "strike": ("attack", "Hit", "dash"),
                  "special": ("skill", "S3", "dash"), "guard": ("guard", "Guard", "brace"),
                  "evade": ("guard", "Evade", "hop"), "hurt": ("hurt", "Damaged"), "stunned": ("hurt", "Damaged")},
        "holds": {"stunned": 2.0},
    },
    "nihil_love": {
        # 原版每单位 50 像素，实际是贴图的 2 倍大；模组图按贴图原尺寸，所以整体缩回一半
        "prefix": "", "base": "nomal", "origin": [80.5, -17.5], "root_scale": 0.5, "weapons": [],
        "anims": {"attack": ("attack", "atk", "dash"), "fire": ("attack", "atk 2", "recoil"),
                  "special": ("skill", "atk 3", "rise"), "guard": ("guard", "guard", "brace"), "hurt": ("hurt", "hit")},
    },
    "nihil_justice": {
        "prefix": "", "base": "nomal", "origin": [63.4, -20.8], "weapons": [],
        "anims": {"attack": ("attack", "trans", "dash"), "hurt": ("hurt", "damaged"), "stunned": ("hurt", "grogi")},
        "holds": {"stunned": 2.0},
    },
    "nihil_happiness": {
        "prefix": "", "origin": [6.5, -1.0], "weapons": [],
        "anims": {"slash": ("attack", "Slash", "step"), "pierce": ("attack", "Hit", "dash"),
                  "special": ("skill", "S1", "dash"), "guard": ("guard", "Guard", "brace"), "hurt": ("hurt", "Damaged")},
    },
    "nihil_courage": {
        "prefix": "", "base": "보통", "origin": [-18.5, 35.0], "weapons": [],
        "anims": {"slash": ("attack", "종", "step"), "strike": ("attack", "횡", "dash"),
                  "special": ("skill", "특수", "dash"), "hurt": ("hurt", "피격")},
    },
    # 宗教层失乐园：场景内嵌 normal / repentance 两个动画库；repentance 期间所有动作都停在忏悔图（原版 S5）
    "lost_paradise": {
        "prefix": "", "origin": [14.0, -57.0], "weapons": [],
        "anims": {"attack": ("attack", "S3", "dash"), "guard": ("guard", "Guard", "brace"),
                  "cast": ("skill", "S1", "rise"), "wake": ("skill", "F", "rise"),
                  "special": ("timeline", {"frames": [[0, "S1"], [1.0, "F"], [2.0, "S3"]], "length": 3.0}, "rise"),
                  "hurt": ("hurt", "Damaged")},
    },
    "lost_paradise_repentance": {
        "layers": "lost_paradise", "prefix": "", "base": "S5", "origin": [9.5, -34.0], "weapons": [],
        "anims": {"hurt": ("hurt", "S5")},
    },
    # 社会层伪王座：模组图是原版放大 2 倍；变形后的待机是 Polymorph_S4，其余动作两种形态相同
    "false_throne": {
        "prefix": "", "origin": [-5.0, -6.5], "root_scale": 2.0, "weapons": [],
        "anims": {"fire": ("attack", "Hit_Fire", "recoil"), "overflowing_light": ("attack", "Penetrate_Fire_S1", "recoil"),
                  "area": ("skill", "AreaAtk_S2", "rise"), "rage": ("skill", "Rage_S3", "step"),
                  "polymorph": ("skill", "Polymorph_S4", "rise"), "guard": ("guard", "Guard", "brace"),
                  "hurt": ("hurt", "Damaged")},
    },
    "false_throne_transformed": {
        "layers": "false_throne", "prefix": "", "base": "Polymorph_S4", "origin": [-21.5, -6.2], "root_scale": 2.0,
        "weapons": [],
        "anims": {"fire": ("attack", "Hit_Fire", "recoil"), "overflowing_light": ("attack", "Penetrate_Fire_S1", "recoil"),
                  "area": ("skill", "AreaAtk_S2", "rise"), "rage": ("skill", "Rage_S3", "step"),
                  "polymorph": ("skill", "Polymorph_S4", "rise"), "guard": ("guard", "Guard", "brace"),
                  "hurt": ("hurt", "Damaged")},
    },
    # 哲学层薄暝：模组图是原版放大 2 倍（F、S3 原版每单位 50 像素，模组运行时再放大 2 倍，骨架按实际大小已经一致）
    "twilight": {
        "prefix": "", "origin": [-6.0, -50.5], "root_scale": 2.0, "weapons": [],
        "anims": {"slash": ("attack", "Slash", "step"), "pierce": ("attack", "Hit", "dash"),
                  "forest_light": ("skill", "BigBird_S1", "rise"), "brilliant_eyes": ("skill", "NormalAndCharm_Fire", "rise"),
                  "punishment": ("attack", "SmallBird_S2", "dash"), "punishment_followup": ("attack", "SmallBird_S3", "dash"),
                  "judgement": ("skill", "LongBird_S4", "rise"), "peace": ("attack", "S5", "dash"),
                  "guard": ("guard", "Guard", "brace"), "hurt": ("hurt", "Damaged")},
    },
    # 特殊来宾卡莉：普通形态与红雾（E.G.O）各一副。血雾五段、尸山两段照场景动画的换图时刻；
    # 模组的打击、斩击图缩小了 0.45 倍并叠着血色弧光，弧光放大回原尺寸后抠成额外层
    "kali": {
        "prefix": "Kali_", "origin": [-193.0, -213.0], "weapons": [],
        "extra": {"Slash": [{"name": "fx", "image": "fx/kali_slash.png", "center": [-167.5, 143.5]}],
                  "Hit": [{"name": "fx", "image": "fx/kali_blunt.png", "center": [-136.5, 366.0]}]},
        "anims": {"slash": ("attack", "Slash", "step"), "blunt": ("attack", "Hit", "dash"),
                  "pierce": ("attack", "Penetrate", "dash"), "move": ("attack", "Move", "dash"),
                  "blood_mist": ("timeline", {"frames": [[0, "S3"], [0.7, "S4"], [1.4, "S5"], [2.1, "Slash2"], [2.8, "Hit2"]],
                                              "length": 3.6}, "dash"),
                  "field_of_corpses": ("timeline", {"frames": [[0, "Slash"]], "length": 1.4}, "step"),
                  "guard": ("guard", "Guard", "brace"), "evade": ("guard", "Evade", "hop"), "hurt": ("hurt", "Damaged")},
    },
    "red_mist": {
        "prefix": "TheRedMist_", "origin": [75.5, 6.0], "weapons": [],
        "extra": {"Slash": [{"name": "fx", "image": "fx/red_mist_slash.png", "center": [-237.5, 444.5]}],
                  "Hit": [{"name": "fx", "image": "fx/red_mist_blunt.png", "center": [-281.5, 331.0]}]},
        "anims": {"slash": ("attack", "Slash", "step"), "blunt": ("attack", "Hit", "dash"),
                  "pierce": ("attack", "Penetrate", "dash"), "move": ("attack", "Move", "dash"),
                  "blood_mist": ("timeline", {"frames": [[0, "S3"], [0.7, "S4"], [1.4, "S5"], [2.1, "Slash2"], [2.8, "Hit2"]],
                                              "length": 3.6}, "dash"),
                  "field_of_corpses": ("timeline", {"frames": [[0, "S1"], [0.7, "S2"]], "length": 1.4}, "dash"),
                  "guard": ("guard", "Guard", "brace"), "evade": ("guard", "Evade", "hop"), "hurt": ("hurt", "Damaged")},
    },
    # 文学层黑天鹅的兄弟：一到五哥共用一套原版人物（无表情），六哥是带笑的另一套；攻击、受击在原版都是整图，
    # 只有待机分了腿、身体、头。模组里一到五哥沉睡/倒下时显示的带黏液待机图不走骨骼（见外观类）
    "swan_bro": {
        "prefix": "BlackSwanBro_", "origin": [-26.5, 4.0], "weapons": [],
        "anims": {"attack": ("attack", "Penetrate", "dash"), "attack_alt": ("attack", "Penetrate", "dash"),
                  "hurt": ("hurt", "Damaged")},
    },
    "swan_bro_smile": {
        "prefix": "BlackSwanBro_", "origin": [-23.5, -32.0], "weapons": [],
        "anims": {"attack": ("attack", "Penetrate", "dash"), "attack_alt": ("attack", "Fire", "recoil"),
                  "hurt": ("hurt", "Damaged")},
    },
    # 历史层终末之光（焦化少女 E.G.O）：模组图没有两只角（原版类型 8 的饰物层），去掉。原版攻击的火环层比模组图里大一倍，
    # 也去掉，换成从模组攻击图抠出的火环：extract_fx.py 带去掉的层定位人物，火环拆成人物身后与从人物前面穿过的两层
    "end_light": {
        "prefix": "ScorchedGirl_", "origin": [-64.0, -112.0], "weapons": [],
        "drop": {"*": ["librarian_only"], "Hit": ["back_acce", "front_acce", "effect", "effect_2", "effect_3"]},
        "extra": {"Hit": [{"name": "fx_back", "image": "fx/end_light_attack_back.png", "center": [-254.5, 259.5], "order": 0},
                          {"name": "fx", "image": "fx/end_light_attack_front.png", "center": [117.5, 249.5]}]},
        "anims": {"attack": ("attack", "Hit", "dash"), "cast": ("guard", "Guard", "brace"),
                  "hurt": ("hurt", "Damaged")},
    },
    # 异想体贪婪国王：国王形态（失去理智）与魔法少女形态各一副，待机分层、动作在原版只有 1–2 层；
    # Special 照场景动画 0.975 秒换第二张。金色琥珀模组图是原版 1.82 倍
    "greed_king": {
        "prefix": "", "origin": [-5.5, -65.0], "weapons": [],
        "anims": {"stab": ("attack", "Hit", "dash"), "slash": ("attack", "Slash", "step"),
                  "special": ("timeline", {"frames": [[0, "Special"], [0.975, "S1"]], "length": 1.95}, "step"),
                  "intro": ("skill", "Special", "rise"), "special_attack": ("attack", "S1", "dash"),
                  "hurt": ("hurt", "Damaged")},
    },
    "greed_girl": {
        "prefix": "", "origin": [6.5, -1.0], "weapons": [],
        "anims": {"stab": ("attack", "Hit", "dash"), "slash": ("attack", "Slash", "step"),
                  "special": ("timeline", {"frames": [[0, "Special"], [0.975, "S1"]], "length": 1.95}, "step"),
                  "intro": ("skill", "Special", "rise"), "special_attack": ("attack", "S1", "dash"),
                  "hurt": ("hurt", "Damaged")},
    },
    "greed_amber": {
        "prefix": "KingOfGreedAmber_", "origin": [-9.0, -1.5], "root_scale": 1.82, "weapons": [],
        "anims": {"hurt": ("hurt", "Damaged")},
    },
    # 扭曲列车的汤吗丽：两个阶段各一副，待机 19 层，攻击、受击、三角铁在原版都是整图
    "tomerry_p1": {
        "prefix": "TomerryPhase1_", "origin": [-38.0, -21.0], "weapons": [],
        "anims": {"strike": ("attack", "Hit", "dash"), "thrust": ("attack", "Penetrate", "dash"),
                  "slash": ("attack", "Slash", "step"), "hurt": ("hurt", "Damaged")},
    },
    "tomerry_p2": {
        "prefix": "TomerryPhase2_", "origin": [-36.5, -21.0], "weapons": [],
        "anims": {"strike": ("attack", "Hit", "dash"), "thrust": ("attack", "Penetrate", "dash"),
                  "slash": ("attack", "Slash", "step"), "triangle": ("attack", "SpecialAtk", "step"),
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

# 原版只有整图的怪物：分层目录由 sprite_layers.py 从模组贴图做出（每张图一个动作，硬部件单独抠层），origin 是它打印的值。
# rigid：只平移、转动，不缩放
_SWORD_IDLE = {"move": {"y": [7, 1, 0.0]}}
for _form, _anims in {
    "normal": {"blunt": ("attack", "NormalBlunt", "dash"), "pierce": ("attack", "NormalPierce", "dash"),
               "slash": ("attack", "NormalSlash", "step"), "parry": ("guard", "NormalParry", "brace"),
               "hurt": ("hurt", "NormalHit")},
    "teardrop": {"blunt": ("attack", "TeardropBlunt", "dash"), "pierce": ("attack", "TeardropPierce", "dash"),
                 "slash": ("attack", "TeardropSlash", "step"), "parry": ("guard", "TeardropParry", "brace"),
                 "hurt": ("hurt", "TeardropHit")},
    "despair": {"attack": ("attack", "DespairAttack", "dash"), "hurt": ("hurt", "DespairHit")},
}.items():
    # 遗忘骑士之剑：悬空的剑，三个形态各一副；待机整把剑绕剑身中点轻晃、上下浮
    BOSSES[f"forgotten_sword_{_form}"] = {
        "layers": "forgotten_sword", "prefix": "", "base": _form.capitalize(), "rigid": True,
        "origin": {"normal": [0.0, -1.0], "teardrop": [0.0, -25.0], "despair": [0.0, -33.0]}[_form],
        "weapons": [{"layers": ["body"], "pivot": [0, 200], "raise": 0, "idle": {"rotate": [1.6, 1, 0.3]}}],
        "idle": _SWORD_IDLE, "anims": _anims,
    }
BOSSES["price_of_silence"] = {
    # 沉默的代价：立着的权杖，待机绕底部慢慢摆
    "prefix": "", "origin": [0.0, -4.0], "rigid": True, "weapons": [],
    "idle": {"Default.body": {"rotate": [1.4, 1, 0.0]}},
    "anims": {"special": ("attack", "Special", "step"), "hurt": ("hurt", "Default")},
}
_BIG_BIRD_ANIMS = {"guard": ("guard", "Guard", "brace"), "charm": ("skill", "Charm"),
                   "rescue": ("attack", "RescueClose", "step"), "hurt": ("hurt", "Hit")}
for _name, _base, _origin in [("big_bird", "Default", [0.0, -17.0]), ("big_bird_sleep", "Sleep", [0.0, -16.0]),
                              ("big_bird_rescue", "RescueOpen", [0.0, -9.0])]:
    # 大鸟：普通、沉睡、救赎三个形态；普通待机的提灯（连光晕）挂在嘴下的提手上像钟摆一样摆。
    # 用户要求爪子始终同一高度：各姿势按爪尖对齐（sprite_layers.py 的 pin_y），动作不转身体、魅惑不上浮
    BOSSES[_name] = {
        "layers": "big_bird", "prefix": "", "base": _base, "origin": _origin, "rigid": True, "no_tilt": True,
        "weapons": ([{"layers": ["lantern"], "pivot": [-151.5, 133.0], "raise": 0, "idle": {"rotate": [5.0, 1, 0.25]}}]
                    if _base == "Default" else []),
        "anims": _BIG_BIRD_ANIMS,
    }
BOSSES["forsaken_murderer"] = {
    # 被遗弃的杀人魔：被铁皮裹住跪着的人，各姿势按膝盖对齐（sprite_layers.py 的 pin），用户要求膝盖始终同一高度。
    # 绕膝盖转会让小腿另一头的脚扎进地面，所以动作只做水平位移（no_tilt），待机只绕膝盖微微晃
    "prefix": "", "origin": [0.0, -207.0], "rigid": True, "weapons": [], "pivot": [4.0, -200.0], "no_tilt": True,
    "preview_on_origin": True,
    "idle": {"Default.body": {"rotate": [0.8, 1, 0.0]}},
    "anims": {"attack": ("attack", "Attack", "dash"), "hurt": ("hurt", "Hit")},
}

# 第二批整图怪物。站在地上的（樵夫、眼珠儿鸟、最后的火柴、渗透天堂、天堂之刺）按脚对齐、动作不转身体（no_tilt），
# 待机只绕脚下微微晃；悬空的时间的痕迹、蹲在笼里的惩戒鸟可以上下浮
BOSSES["time_trace"] = {
    "prefix": "", "origin": [0.0, -5.0], "rigid": True, "weapons": [],
    "idle": {"move": {"y": [6, 1, 0.0]}, "Default.body": {"rotate": [1.5, 1, 0.3]}},
    "anims": {"blunt": ("attack", "AttackBlunt", "dash"), "thrust": ("attack", "AttackThrust", "dash"),
              "slash": ("attack", "AttackSlash", "step"), "guard": ("guard", "Guard", "brace"),
              "hurt": ("hurt", "Hit"), "stun": ("hurt", "Hit")},
    "holds": {"stun": 0.8},
}
_WOODSMAN_ANIMS = {"blunt": ("attack", "AttackBlunt", "dash"), "slash": ("attack", "AttackSlash", "step"),
                   "logging": ("attack", "LoggingFinal", "step"), "guard": ("guard", "Guard", "brace"),
                   "hurt": ("hurt", "Hit")}
for _base in ("Empty", "Warm"):
    # 热心的樵夫：没有心、有心两张待机各一副
    BOSSES[f"woodsman_{_base.lower()}"] = {
        "layers": "woodsman", "prefix": "", "base": _base, "origin": [0.0, 0.0], "rigid": True, "no_tilt": True,
        "weapons": [], "idle": {f"{_base}.body": {"rotate": [0.8, 1, 0.0]}}, "anims": _WOODSMAN_ANIMS,
    }
BOSSES["woodsman_tree"] = {
    "prefix": "", "origin": [0.0, 0.0], "rigid": True, "no_tilt": True, "weapons": [],
    "idle": {"Default.body": {"rotate": [1.0, 1, 0.0]}},
    "anims": {"guard": ("guard", "Default", "brace"), "hurt": ("hurt", "Default")},
}
BOSSES["eyeball_bird"] = {
    "prefix": "", "origin": [0.0, -1.0], "rigid": True, "no_tilt": True, "weapons": [],
    "idle": {"Default.body": {"rotate": [1.2, 1, 0.0]}},
    "anims": {"attack": ("attack", "Attack", "dash"), "evade": ("guard", "Evade", "hop"), "hurt": ("hurt", "Hit")},
}
BOSSES["punishing_bird"] = {
    "prefix": "", "origin": [0.0, 0.0], "rigid": True, "weapons": [],
    "idle": {"move": {"y": [4, 1, 0.0]}, "Default.body": {"rotate": [2.0, 1, 0.25]}},
    "anims": {"peck": ("attack", "Peck", "dash"), "punish": ("attack", "Punish", "step"), "hurt": ("hurt", "Hit")},
}
BOSSES["last_match"] = {
    "prefix": "", "origin": [0.0, 0.0], "rigid": True, "no_tilt": True, "weapons": [],
    "idle": {"Default.body": {"rotate": [1.0, 1, 0.0]}},
    "anims": {"attack": ("attack", "Attack", "dash"), "cast": ("skill", "Cast"), "hurt": ("hurt", "Hit")},
}
for _name, _anims in (("burrowing_heaven", {"attack": ("attack", "Attack", "step"), "special": ("skill", "Special"),
                                            "guard": ("guard", "Guard", "brace"), "hurt": ("hurt", "Hit")}),
                      ("heaven_thorn", {"attack": ("attack", "Attack", "step"), "guard": ("guard", "Guard", "brace"),
                                        "hurt": ("hurt", "Hit")})):
    for _base in ("Awake", "Sleep"):
        # 渗透天堂、天堂之刺：醒着、睡着两张待机各一副，待机绕根部慢慢摇
        BOSSES[f"{_name}_{_base.lower()}"] = {
            "layers": _name, "prefix": "", "base": _base, "origin": [0.0, 0.0], "rigid": True, "no_tilt": True,
            "weapons": [], "idle": {f"{_base}.body": {"rotate": [1.2, 1, 0.0]}}, "anims": _anims,
        }

# 第三批：硬物和僵硬的人偶。原点、各形态缩放读 sprite_layers.py 写出的 pin_ref.json（origin: "auto"），
# 对齐点、转轴、是否转身体读标注页（pins/）；选了“不钉”的标注点不用
def _rigid(idle, anims, **kw):
    return {"prefix": "", "origin": "auto", "rigid": True, "weapons": [], "idle": idle, "anims": anims, **kw}


BOSSES["emerald_crystal"] = _rigid({"Default.body": {"rotate": [1.2, 1, 0.0]}, "move": {"y": [4, 1, 0.25]}},
                                   {"attack": ("attack", "Default", "step"), "guard": ("guard", "Default", "brace"),
                                    "hurt": ("hurt", "Default")})
BOSSES["shining_happiness"] = _rigid({"Default.body": {"rotate": [1.5, 1, 0.0]}, "move": {"y": [5, 1, 0.25]}},
                                     {"hurt": ("hurt", "Default")})
BOSSES["nf_shining_happiness"] = _rigid({"Default.body": {"rotate": [1.5, 1, 0.0]}, "move": {"y": [5, 1, 0.25]}},
                                        {"hurt": ("hurt", "Default")})
BOSSES["road_home_house"] = _rigid({"Default.body": {"rotate": [0.6, 1, 0.0]}},
                                   {"guard": ("guard", "Default", "brace"), "hurt": ("hurt", "Default")})
BOSSES["vine_barrier"] = _rigid({"Default.body": {"rotate": [1.4, 1, 0.0]}},
                                {"guard": ("guard", "Default", "brace"), "hurt": ("hurt", "Default")},
                                preview_on_origin=True)
for _n in ("hermit_staff", "nf_hermit_staff"):
    BOSSES[_n] = _rigid({"Default.body": {"rotate": [1.2, 1, 0.0]}},
                        {"attack": ("attack", "Attack", "step"), "hurt": ("hurt", "Hit")})
for _n in ("gift_box", "lf_gift_box"):
    BOSSES[_n] = _rigid({"Default.body": {"rotate": [1.5, 1, 0.0]}}, preview_on_origin=_n == "gift_box", anims=
                        {"attack": ("attack", "Attack", "dash"), "cast": ("skill", "Cast"), "hurt": ("hurt", "Hit"),
                         # 文学层的自爆照场景动画：施法 → 0.32 秒攻击 → 0.62 秒受击，共 0.9 秒
                         "self_destruct": ("timeline", {"frames": [[0, "Cast"], [0.32, "Attack"], [0.62, "Hit"]],
                                                        "length": 0.9}, "dash")})
for _base in ("Default", "Nova"):
    # 蓝星祭坛：普通、新星两个动画库各一副；石台很重，待机几乎不动
    BOSSES["blue_star_altar" + ("" if _base == "Default" else "_nova")] = _rigid(
        {f"{_base}.body": {"rotate": [0.3, 1, 0.0]}},
        {"nova": ("skill", "Nova"), "hurt": ("hurt", _base)}, layers="blue_star_altar", base=_base)
# 自然层遗忘骑士之剑：普通、泪滴、绝望、倒下四个动画库各一副；绝望库所有动作都用攻击图或待机图，倒下库只有一张
_NF_SWORD = {"normal": "Normal", "teardrop": "Teardrop"}
for _lib, _p in _NF_SWORD.items():
    BOSSES[f"nf_forgotten_sword_{_lib}"] = _rigid(
        {"move": {"y": [7, 1, 0.0]}, f"{_p}.body": {"rotate": [1.6, 1, 0.3]}},
        {"slash": ("attack", f"{_p}Slash", "step"), "blunt": ("attack", f"{_p}Blunt", "dash"),
         "pierce": ("attack", f"{_p}Pierce", "dash"), "guard": ("guard", f"{_p}Guard", "brace"),
         "evade": ("guard", f"{_p}Evade", "hop"), "hurt": ("hurt", f"{_p}Hit")},
        layers="nf_forgotten_sword", base=_p)
BOSSES["nf_forgotten_sword_despair"] = _rigid(
    {"move": {"y": [7, 1, 0.0]}, "Despair.body": {"rotate": [1.6, 1, 0.3]}},
    {"attack": ("attack", "DespairAttack", "dash"), "guard": ("guard", "Despair", "brace"), "hurt": ("hurt", "Despair")},
    layers="nf_forgotten_sword", base="Despair")
BOSSES["nf_forgotten_sword_dead"] = _rigid({}, {"hurt": ("hurt", "Dead")}, layers="nf_forgotten_sword", base="Dead")

# 第四批：硬壳的虫、鸟和南瓜头
# 蜂后：坐在裙状的身体上不动，只有待机图的四片翅膀（sprite_layers.py 抠出、画在身体后面）绕翅根扇；
# 左右两侧相位差半拍，像同时往外张。施法在外观里就是防御图
BOSSES["queen_bee"] = _rigid(
    {"Default.body": {"rotate": [0.5, 1, 0.0]}},
    {"defend": ("guard", "Defend", "brace"), "cast": ("skill", "Defend"), "hurt": ("hurt", "Hit")},
    preview_on_origin=True,
    weapons=[{"layers": [f"wing_{n}"], "pivot": p, "raise": 0, "idle": {"rotate": [a, 3, ph]}}
             for n, p, a, ph in (("ul", [-72, 92], 7.0, 0.0), ("ur", [98, 102], 7.0, 0.5),
                                 ("ll", [-122, -48], 5.0, 0.08), ("lr", [178, -43], 5.0, 0.58))])
# 工蜂（历史层、异想体共用）：站着的大虫，待机绕脚下轻晃；两种攻击都是冲上前
BOSSES["worker_bee"] = _rigid(
    {"Default.body": {"rotate": [1.0, 1, 0.0]}},
    {"attack": ("attack", "Attack", "dash"), "attack2": ("attack", "Attack2", "dash"),
     "dodge": ("guard", "Dodge", "hop"), "hurt": ("hurt", "Hit")},
    preview_on_origin=True)
# 守林鸟（左右两只同一外观）：一团黑羽毛，突刺冲上前、斩击踏一步。骨架照原图朝左，游戏里随贴图 Flip() 镜像
BOSSES["forest_keeper_bird"] = _rigid(
    {"Default.body": {"rotate": [1.0, 1, 0.0]}},
    {"thrust": ("attack", "Thrust", "dash"), "slash": ("attack", "Slash", "step"), "hurt": ("hurt", "Hit")})
# 杰克（南瓜头）：休眠、苏醒两张待机各一副。"Awake" 触发先切形态再播（SpineSpriteAttackCreatureVisuals.PlaySpineTrigger），
# 所以苏醒动作放在苏醒那副里：先显示趴着的一团，再站起来
BOSSES["ozma_jack_dormant"] = _rigid(
    {"Dormant.body": {"rotate": [0.6, 1, 0.0]}},
    {"hurt": ("hurt", "Hit")}, layers="ozma_jack", base="Dormant")
BOSSES["ozma_jack_awake"] = _rigid(
    {"Awake.body": {"rotate": [1.2, 1, 0.0]}},
    {"awake": ("timeline", {"frames": [[0, "Dormant"], [0.25, "Awake"]], "length": 0.5}, "step"),
     "hurt": ("hurt", "Hit")}, layers="ozma_jack", base="Awake")

# 异想体今天也很害羞：原版每个动作的 5 层是 5 种表情的全身整图叠在一起（1 怒 … 5 笑，与模组表情编号一致），
# 没有身体部件。每种表情一副骨架、只留该表情那层，动作只靠整体位移和倾斜
for _e in range(1, 6):
    BOSSES[f"shy_look_{_e}"] = {
        "layers": "shy_look", "prefix": "ShyLookToday_", "origin": [-33.0, 6.0], "weapons": [],
        "drop": {"*": [str(i) for i in range(1, 6) if i != _e]},
        "anims": {"attack": ("attack", "Atk", "step"), "cast": ("skill", "Default", "rise"),
                  "hurt": ("hurt", "Damaged")},
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


# 场景动画（文学层）照搬的换图时间线：frames 是 [时刻, 姿势]，length 后回待机；怪物代码按契约里的命中时刻结算伤害，
# 所以换姿势的时刻不能改。整体按动作类型位移，每个命中时刻再往前顶一下（hits）。
TIMELINE_BASE = {"dash": -70, "step": -25}


def timeline(D, tl, style):
    frames = []
    for t, p in tl["frames"]:
        if not frames or frames[-1][1] != p:  # 场景里同一张图连着设两次，只算一段
            frames.append((t, p))
    length = tl["length"]
    end = length + 0.3
    show = (([[0, D, 0]] if frames[0][0] > 0 else []) + [[round(t, 3), p, 0] for t, p in frames]
            + [[round(length, 3), D, 0]])
    keys = {}

    def add(target, channel, ks):
        keys.setdefault(target, {}).setdefault(channel, []).extend(ks)

    bounds = [t for t, _ in frames[1:]] + [length]
    for (t0, p), t1 in zip(frames, bounds):
        step = t1 - t0
        add(f"{p}.body", "x", [[t0, 8], [t0 + min(0.15, step * 0.5), -6, "out"], [t0 + min(0.4, step), 0]])
        for target, ch in lag(p, t0).items():
            add(target, "rotate", [k for k in ch["rotate"] if k[0] <= t1])
    base = TIMELINE_BASE.get(style, 0)
    xs = [[0, 0], [0.15, base, "out"]]
    for h in tl.get("hits", []):
        if h - 0.08 > xs[-1][0]:
            xs += [[h - 0.08, base], [h, base - 22, "out"], [min(h + 0.2, length), base]]
    xs += [[length, base], [end, 0]]
    keys["move"] = {"x": [k for i, k in enumerate(xs) if i == 0 or k[0] > xs[i - 1][0]]}
    if style == "rise":
        keys["move"]["y"] = [[0, 0], [0.25, 14, "out"], [length, 14], [end, 0]]
    for chs in keys.values():
        for c, ks in chs.items():
            ks.sort(key=lambda k: k[0])
    return {"duration": round(end, 3), "show": show, "keys": keys}


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
        hold = spec.get("holds", {}).get(name) or (SEQ_STEP * len(pose) if kind == "seq" else HOLD.get(kind, 0))
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
        elif kind == "timeline":
            out[name] = timeline(D, pose, style)
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
    # idle：额外的待机摆动（悬空的剑上下浮、提灯摆），合并进待机的 osc
    for target, chs in spec.get("idle", {}).items():
        out["idle"]["osc"].setdefault(target, {}).update(chs)
    # rigid：只有整图的怪物（sprite_layers.py）整块平移、转动，不缩放。整张图一起伸缩看起来像软胶，用户不要
    # no_tilt：身体骨头不转、不上下动（跪地、站地的整图一转，着地的另一头就会扎进地面），只留水平位移；
    # 待机的 idle 摆动不受影响
    if spec.get("no_tilt"):
        for name, a in out.items():
            for target, chs in a.get("keys", {}).items():
                if target.endswith(".body"):
                    chs.pop("rotate", None)
                    chs.pop("y", None)
    if spec.get("rigid"):
        for a in out.values():
            for group in ("keys", "osc"):
                for chs in a.get(group, {}).values():
                    chs.pop("sx", None)
                    chs.pop("sy", None)
    return out


def main():
    out_dir = Path(sys.argv[1])
    only = set(sys.argv[2:])
    for name, spec in BOSSES.items():
        if only and name not in only:
            continue
        src_dir = spec.get("layers", name)
        info = json.loads((LAYERS / src_dir / "layers.json").read_text())
        ref_path = LAYERS / src_dir / "pin_ref.json"
        if ref_path.exists():
            # 标注页的动作开关（sprite_layers.py 写出）：“动作不转身体”覆盖 no_tilt，“当转轴”把身体转轴放到参照点
            ref = json.loads(ref_path.read_text())
            if "noTilt" in ref:
                spec = {**spec, "no_tilt": ref["noTilt"]}
            if ref.get("pivot") and ref.get("ref"):
                spec = {**spec, "pivot": ref["ref"]}
            if spec.get("origin") == "auto":
                base_pose = spec.get("base", "Default")
                spec = {**spec, "origin": ref["origins"][base_pose],
                        **({"root_scale": ref["root_scales"][base_pose]} if base_pose in ref.get("root_scales", {}) else {})}
        used = set()
        for _, p, *_ in spec["anims"].values():
            used.update([f[1] for f in p["frames"]] if isinstance(p, dict) else p if isinstance(p, list) else [p])
        base = spec.get("base", "Default")
        poses = [base] + sorted(used - {base})
        motions = {}
        for pose in poses:
            src = spec["prefix"] + pose
            drop = spec.get("drop", {}).get(pose, spec.get("drop", {}).get("*", []))
            bones, assign, _ = rig_motion(LAYERS / src_dir, src, [l for l in info[src]["layers"] if l["name"] not in drop])
            if "pivot" in spec:
                # 身体转动的轴（默认在头正下方的地面）；跪着的杀人魔放在膝盖，转动时膝盖不离地
                bones["body"]["at"] = spec["pivot"]
            extra = spec.get("extra", {}).get(pose, [])
            for ex in extra:
                assign[ex["name"]] = ex.get("bone", "body") if ex.get("bone", "body") in bones else "body"
            if pose == base:
                for i, w in enumerate(spec["weapons"]):
                    role = "weapon" + ("" if i == 0 else str(i + 1))
                    bones[role] = {"parent": "body", "at": w["pivot"]}
                    for l in w["layers"]:
                        assign[l] = role
            motions[pose] = {"source": src, "bones": bones, "assign": assign, **({"extra": extra} if extra else {}),
                             **({"drop": drop} if drop else {})}
        cfg = {"name": f"boss_{name}", "layers_dir": f"~/.local/share/LibraryOfRuina-layers/{src_dir}", "texture_scale": 0.6,
               **({"root_scale": spec["root_scale"]} if "root_scale" in spec else {}),
               # 预览的地面线默认放坐标原点（外观的贴图 Position）；Position 在贴图中心的（被遗弃的杀人魔）改放待机图底边
               **({"preview_on_origin": True} if spec.get("preview_on_origin") else {}),
               "origin": spec["origin"], "skin_tint": [1.0, 1.0, 1.0], "fps": 30, "setup_motion": base,
               "motions": motions, "animations": animations(spec)}
        (out_dir / f"boss_{name}.json").write_text(json.dumps(cfg, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
        print(name, list(cfg["animations"]))


if __name__ == "__main__":
    main()
