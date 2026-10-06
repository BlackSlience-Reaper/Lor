"""原版只有整图的怪物：把模组的逐帧贴图做成和 export_layers.py 同格式的分层目录，再走 build_boss_configs 的骨架流程。
每张贴图一个动作（一层 body），按模组换图外观的摆放规则放到同一坐标系里，换姿势时位置与原来的逐帧换图一致；
硬的小部件（大鸟的提灯）可以按椭圆加亮度阈值抠成单独的层，挂自己的骨头摆动。

坐标：以外观的贴图 Position 为原点，y 向上，单位是待机贴图像素。摆放规则同 SpriteAttackCreatureVisuals：
- visible_bottom（默认锚点）：贴图可见底边（alpha > 0.03 的最低行）的中点落在 Position + Nudge；
- center（Profile.Centered()）：贴图中心落在 Position + Nudge。
Nudge 是父节点单位，除以贴图缩放换成待机像素；Lunge 招式不用 Nudge。帧另给 Scale 时按 帧缩放/待机缩放 缩放贴图。
骨架原点要放在该形态待机贴图的矩形底边中点（RuntimeSpineBody.AlignTo），打印在 origins 里，填进 build_boss_configs。
对齐点标注见 pins/：标注页 https://claude.ai/artifact/XpeiYUEg1JKXCgBk9EibZb 读回的 {mode, pivot, noTilt, points}。
用法：sprite_layers.py [名字...]"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

M = Path(__file__).resolve().parents[2] / "images/monsters"
LAYERS = Path.home() / ".local/share/LibraryOfRuina-layers"
# 标注页（怪物着地点标注）读回的对齐点、对齐方式与动作开关，一个怪物一份
PINS = Path(__file__).resolve().parent / "pins"

SPRITES = {
    # 外观 ForgottenKnightSwordCreatureVisuals：三个形态，攻击帧 Nudge(-46, -10)。
    # 打击、斩击、突刺三张图里的剑画得只有待机的约一半（打击、斩击剑柄头到剑尖约 190 像素，待机约 350；突刺按剑柄比例约 0.5），
    # 换姿势时剑忽大忽小，fix 先粗略放大回去；最终大小按标注页的“尺寸两端”（剑柄头到剑尖）标定，
    # 三个形态都对到普通待机的剑长（绝望形态原图大约 1.22 倍，也一起缩回）
    "forgotten_sword": {
        "scale": 0.54, "anchor": "visible_bottom", "size_like": {"TeardropBlunt": "NormalBlunt"},
        "idles": {"Normal": "forgotten_knight_sword/normal_idle.png", "Teardrop": "forgotten_knight_sword/teardrop_idle.png",
                  "Despair": "forgotten_knight_sword/despair_idle.png"},
        "poses": {
            "NormalBlunt": {"image": "forgotten_knight_sword/normal_blunt.png", "nudge": [-46, -10], "fix": 1.85},
            "NormalPierce": {"image": "forgotten_knight_sword/normal_pierce.png", "nudge": [-46, -10], "fix": 2.0},
            "NormalSlash": {"image": "forgotten_knight_sword/normal_slash.png", "nudge": [-46, -10], "fix": 1.85},
            "NormalParry": {"image": "forgotten_knight_sword/normal_parry.png"},
            "NormalHit": {"image": "forgotten_knight_sword/normal_hit.png"},
            "TeardropBlunt": {"image": "forgotten_knight_sword/teardrop_blunt.png", "nudge": [-46, -10], "fix": 1.85},
            "TeardropPierce": {"image": "forgotten_knight_sword/teardrop_pierce.png", "nudge": [-46, -10], "fix": 2.0},
            "TeardropSlash": {"image": "forgotten_knight_sword/teardrop_slash.png", "nudge": [-46, -10], "fix": 1.85},
            "TeardropParry": {"image": "forgotten_knight_sword/teardrop_parry.png"},
            "TeardropHit": {"image": "forgotten_knight_sword/teardrop_hit.png"},
            "DespairAttack": {"image": "forgotten_knight_sword/despair_attack.png", "nudge": [-46, -10]},
            "DespairHit": {"image": "forgotten_knight_sword/despair_hit.png"},
        },
    },
    # PriceOfSilenceCreatureVisuals：特殊帧 Nudge(-18, 10)
    "price_of_silence": {
        "scale": 0.56, "anchor": "visible_bottom",
        "idles": {"Default": "price_of_silence/idle.png"},
        "poses": {"Special": {"image": "price_of_silence/special.png", "nudge": [-18, 10]}},
    },
    # BigBirdCreatureVisuals：普通、沉睡、救赎（张开）三个形态；普通待机的提灯抠出来摆动（暗色的爪子留在身体上）
    "big_bird": {
        # 救赎形态的红提灯垂得比爪子低，按可见底边对齐时爪子会悬空；用户要求爪子始终同一高度，pin_y 按爪尖对齐
        "scale": 0.60, "anchor": "visible_bottom", "pin_y": "dark_bottom",
        # 防御、魅惑、救赎张开三张图里的鸟画得比待机小（按黑色身体的面积和高度量，约 1.33、1.45、1.15 倍），
        # 原来逐帧换图时会忽大忽小；fix 把鸟放大回待机的大小
        "idles": {"Default": "big_bird/idle.png", "Sleep": "big_bird/sleep.png",
                  "RescueOpen": {"image": "big_bird/rescue_open.png", "fix": 1.15}},
        "poses": {"Hit": {"image": "big_bird/hit.png"}, "Guard": {"image": "big_bird/guard.png", "fix": 1.33},
                  "Charm": {"image": "big_bird/charm.png", "fix": 1.45},
                  "RescueClose": {"image": "big_bird/rescue_close.png"}},
        # 光晕盖在爪子上、爪子又挡着光晕，抠出来会带爪形缺口：身体上去掉椭圆里所有亮像素（爪子是暗的，留下），
        # 提灯层是提灯本体（box）加一圈按原图取色重画的径向光晕，跟着提灯一起摆
        "cutouts": {"Default": [{"name": "lantern", "ellipse": [100, 405, 108, 108], "min_lum": 110,
                                 "box": [78, 364, 126, 440],
                                 "glow": {"center": [100, 405], "radius": 98, "core": 0.42, "alpha": 250,
                                          "inner": [255, 246, 206], "outer": [255, 224, 70]}}]},
    },
    # ForsakenMurdererCreatureVisuals：Centered，攻击是 Lunge（不用 Nudge）、Scale(0.33)。
    # 跪着的人：按图片中心对齐时换姿势膝盖会上下跳，用户要求膝盖始终在同一高度——pin 是各图里膝盖着地的点
    # （束缚衣最下面一段的左端），各姿势按它对齐到待机图的膝盖
    "forsaken_murderer": {
        "scale": 0.31, "anchor": "center",
        "idles": {"Default": {"image": "forsaken_murderer.webp", "pin": [320, 407]}},
        "poses": {"Attack": {"image": "forsaken_murderer_attack.webp", "frame_scale": 0.33, "pin": [750, 652]},
                  "Hit": {"image": "forsaken_murderer_hit.webp", "pin": [30, 693]}},
    },
    # ---- 第二批 ----
    # TimeTraceCreatureVisuals：悬空持镰的影子
    "time_trace": {
        "scale": 1.24, "anchor": "visible_bottom",
        "idles": {"Default": "time_trace/idle.png"},
        "poses": {"AttackBlunt": {"image": "time_trace/attack_blunt.png"},
                  "AttackThrust": {"image": "time_trace/attack_thrust.png"},
                  "AttackSlash": {"image": "time_trace/attack_slash.png"},
                  "Hit": {"image": "time_trace/hit.png"}, "Guard": {"image": "time_trace/guard.png"}},
    },
    # WarmheartedWoodsmanCreatureVisuals：两张待机按 Scale(1.2).IdleOnly() 画在原点，动作图按布局 0.87 倍、(0, -8)。
    # 动作图里的樵夫因此只有待机的 0.725 倍，换姿势忽大忽小：fix 1.38 放大回去，横向 Nudge 跟着放大；脚按 pin_y 对齐
    "woodsman": {
        "scale": 1.2, "attack_scale": 0.87, "attack_offset": [0, -8], "anchor": "visible_bottom",
        "pin_y": "dark_bottom",
        "idles": {"Empty": "warmhearted_woodsman/idle_empty.png", "Warm": "warmhearted_woodsman/idle_warm.png"},
        "poses": {"AttackBlunt": {"image": "warmhearted_woodsman/attack_blunt.png", "nudge": [124, 35], "fix": 1.38},
                  "AttackSlash": {"image": "warmhearted_woodsman/attack_slash.png", "nudge": [-14, 35], "fix": 1.38},
                  "LoggingFinal": {"image": "warmhearted_woodsman/logging_final.png", "nudge": [-262, 145], "fix": 1.38},
                  "Hit": {"image": "warmhearted_woodsman/hit.png", "fix": 1.38},
                  "Guard": {"image": "warmhearted_woodsman/guard.png", "fix": 1.38}},
    },
    "woodsman_tree": {
        "scale": 0.36, "anchor": "visible_bottom", "pin_y": "dark_bottom",
        "idles": {"Default": "warmhearted_woodsman/tree.png"}, "poses": {},
    },
    # BigBird 文件里的 EyeballBirdCreatureVisuals
    "eyeball_bird": {
        "scale": 0.56, "anchor": "visible_bottom", "pin_y": "dark_bottom",
        "idles": {"Default": "eyeball_bird/idle.png"},
        "poses": {"Attack": {"image": "eyeball_bird/attack.png"}, "Evade": {"image": "eyeball_bird/evade.png"},
                  "Hit": {"image": "eyeball_bird/hit.png"}},
    },
    # PunishingBirdCreatureVisuals：鸟本体 At(-90, -290)、Scale(1)，动作图同样位置缩放（笼子、锁链是外观另挂的节点，不进骨架）
    "punishing_bird": {
        "scale": 1.0, "anchor": "visible_bottom",
        "idles": {"Default": "punishing_bird/idle.png"},
        "poses": {"Peck": {"image": "punishing_bird/peck.png"}, "Punish": {"image": "punishing_bird/punish.png"},
                  "Hit": {"image": "punishing_bird/hit.png"}},
    },
    # HistoryFloorLastMatchCreatureVisuals：攻击是 Lunge、Scale(0.40)
    "last_match": {
        "scale": 0.38, "anchor": "visible_bottom", "pin_y": "dark_bottom",
        "idles": {"Default": "history_floor/last_match.webp"},
        "poses": {"Attack": {"image": "history_floor/last_match_attack.png", "frame_scale": 0.40},
                  "Cast": {"image": "history_floor/last_match_cast.png"},
                  "Hit": {"image": "history_floor/last_match_hit.png"}},
    },
    "burrowing_heaven": {
        "scale": 0.58, "anchor": "visible_bottom", "pin_y": "dark_bottom",
        "idles": {"Awake": "burrowing_heaven/idle_awake.png", "Sleep": "burrowing_heaven/idle_sleep.png"},
        "poses": {"Attack": {"image": "burrowing_heaven/attack.png"}, "Hit": {"image": "burrowing_heaven/hit.png"},
                  "Special": {"image": "burrowing_heaven/special.png"}, "Guard": {"image": "burrowing_heaven/guard.png"}},
    },
    "heaven_thorn": {
        "scale": 0.48, "anchor": "visible_bottom", "pin_y": "dark_bottom",
        "idles": {"Awake": "heaven_thorn/idle_awake.png", "Sleep": "heaven_thorn/idle_sleep.png"},
        "poses": {"Attack": {"image": "heaven_thorn/attack.png"}, "Hit": {"image": "heaven_thorn/hit.png"},
                  "Guard": {"image": "heaven_thorn/guard.png"}},
    },
    # ---- 第三批：硬物 ----
    "emerald_crystal": {"scale": 0.38, "anchor": "visible_bottom",
                        "idles": {"Default": "social_floor_liberation/emerald_crystal/default.png"}, "poses": {}},
    "shining_happiness": {"scale": 0.50, "anchor": "visible_bottom",
                          "idles": {"Default": "king_of_greed/shining_happiness.png"}, "poses": {}},
    "road_home_house": {"scale": 0.62, "anchor": "visible_bottom", "idles": {"Default": "road_home/house.png"}, "poses": {}},
    # HistoryFloorVineBarrierCreatureVisuals：Centered，待机 At(0, -88)、Scale(0.4)，只有一张
    "vine_barrier": {"scale": 0.4, "anchor": "center",
                     "idles": {"Default": "history_floor/emerald_bough/vine_barrier_idle.png"}, "poses": {}},
    # HermitStaffCreatureVisuals：待机 Scale(0.515)（不是 IdleOnly，动作图同样 0.515）
    "hermit_staff": {"scale": 0.515, "anchor": "visible_bottom",
                     "idles": {"Default": "hermit_staff/idle.png"},
                     "poses": {"Attack": {"image": "hermit_staff/attack.png"}, "Hit": {"image": "hermit_staff/hit.png"}}},
    # SurpriseGiftBoxCreatureVisuals：Centered；攻击是 Lunge，终点在 Nudge(23.4, -106.6)、Scale(0.65)
    "gift_box": {"scale": 0.806, "anchor": "center",
                 "idles": {"Default": "leticia/surprise_gift_box_idle.png"},
                 # 攻击图里的人偶和待机图原图一样大，却按 0.65 倍画（待机 0.806），出招时缩成八成：fix 放大回去
                 "poses": {"Attack": {"image": "leticia/surprise_gift_box_attack.png", "nudge": [23.4, -106.6], "frame_scale": 0.65,
                                      "fix": 1.24},
                           "Cast": {"image": "leticia/surprise_gift_box_cast.png"},
                           "Hit": {"image": "leticia/surprise_gift_box_hit.png"}}},
    # 以下原来是场景动画：摆放读场景（scene_placements）
    "nf_hermit_staff": {"scene": "natural_floor_hermit_staff.tscn", "ref_library": "main",
                        "libraries": {"main": "natural_floor_hermit_staff_animations.tres"},
                        "idles": {"Default": "main"},
                        "poses": {"Attack": ["main", "Attack", 0], "Hit": ["main", "Hit", 0]}},
    "nf_shining_happiness": {"scene": "natural_floor_shining_happiness.tscn", "ref_library": "happiness",
                             "libraries": {"happiness": "natural_floor_shining_happiness_animations.tres"},
                             "idles": {"Default": "happiness"}, "poses": {}},
    "lf_gift_box": {"scene": "literature_floor_surprise_gift_box.tscn", "ref_library": "main",
                    "libraries": {"main": "literature_floor_surprise_gift_box_animations.tres"},
                    "idles": {"Default": "main"},
                    "poses": {"Attack": ["main", "Attack", 0, 1.24], "Cast": ["main", "Cast", 0], "Hit": ["main", "Hit", 0]}},
    "blue_star_altar": {"scene": "blue_star_altar.tscn", "ref_library": "normal",
                        "libraries": {"normal": "blue_star_altar_normal_animations.tres",
                                      "nova": "blue_star_altar_nova_animations.tres"},
                        "idles": {"Default": "normal", "Nova": "nova"}, "poses": {}},
    # 自然层遗忘骑士之剑：普通、泪滴、绝望、倒下四个动画库（倒下只有一张受击图），泪滴库按 1 倍画、其余 0.5 倍
    "nf_forgotten_sword": {"scene": "natural_floor_forgotten_sword.tscn", "ref_library": "normal",
                           "libraries": {f: f"natural_floor_forgotten_sword_{f}_animations.tres"
                                         for f in ("normal", "teardrop", "despair", "dead")},
                           "idles": {"Normal": "normal", "Teardrop": "teardrop", "Despair": "despair", "Dead": "dead"},
                           "poses": {**{f"Normal{a}": ["normal", a, 0] for a in ("Blunt", "Pierce", "Slash", "Guard", "Evade", "Hit")},
                                     **{f"Teardrop{a}": ["teardrop", a, 0] for a in ("Blunt", "Pierce", "Slash", "Guard", "Evade", "Hit")},
                                     "DespairAttack": ["despair", "Attack", 0]}},
    # ---- 第四批 ----
    # QueenBeeCreatureVisuals：Centered，布局 0.46；施法用的是防御图（cast.png 没被用到）
    "queen_bee": {"scale": 0.46, "anchor": "center",
                  "idles": {"Default": "queen_bee/idle.png"},
                  # 待机的四片翅膀（灰白、在身体后面）抠成单独的层，各绕翅根扇动
                  "cutouts": {"Default": [
                      {"name": f"wing_{n}", "ellipse": e, "away": 8, "min_lum": 0, "behind": True}
                      for n, e in (("ul", [320, 345, 130, 125]), ("ur", [770, 345, 145, 130]),
                                   ("ll", [310, 565, 85, 85]), ("lr", [785, 560, 110, 90]))]},
                  "poses": {"Defend": {"image": "queen_bee/defend.png"}, "Hit": {"image": "queen_bee/hit.png"}}},
    # HistoryFloorWorkerBeeCreatureVisuals：Centered，待机 0.44；两种攻击是 Lunge、Scale(0.50)（Nudge 是冲刺终点，不算摆放）。
    # 异想体版 QueenBeeWorkerCreatureVisuals 同一套图，只是闪避、受击按布局 0.54 画，比待机大 1.23 倍；两版共用这副骨架
    "worker_bee": {"scale": 0.44, "anchor": "center",
                   "idles": {"Default": "history_floor/worker_bee_idle.png"},
                   "poses": {"Attack": {"image": "history_floor/worker_bee_attack.png", "frame_scale": 0.50},
                             "Attack2": {"image": "history_floor/worker_bee_attack2.png", "frame_scale": 0.50},
                             "Dodge": {"image": "history_floor/worker_bee_dodge.png"},
                             "Hit": {"image": "history_floor/worker_bee_hit.png"}}},
    # ForestKeeperBirdCreatureVisuals（左右两只同一外观）：全部 Scale(0.594)，变体 Flip() 连动作图一起左右翻（游戏里朝右）。
    # 骨架照原图朝左做，游戏里 RuntimeSpineBody.AlignTo 照待机贴图的 FlipH 镜像；标注页给的是翻过的图，
    # points_flipped 把标注点镜像回原图坐标
    "forest_keeper_bird": {"scale": 0.594, "anchor": "visible_bottom", "points_flipped": True,
                           "idles": {"Default": "forest_keeper_bird/idle.png"},
                           "poses": {"Thrust": {"image": "forest_keeper_bird/thrust.png"},
                                     "Slash": {"image": "forest_keeper_bird/slash.png"},
                                     "Hit": {"image": "forest_keeper_bird/hit.png"}}},
    # OzmaJackCreatureVisuals：休眠、苏醒两个形态，受击图两形态共用
    "ozma_jack": {"scale": 0.48, "anchor": "visible_bottom",
                  "idles": {"Dormant": "ozma/jack_dormant.png", "Awake": "ozma/jack_awake.png"},
                  "poses": {"Hit": {"image": "ozma/jack_hit.png"}}},
    # ---- 第五批 ----
    # ScowlingFaceCreatureVisuals：布局 0.30；各图的人物不在图中间，AnchorX 把每张图的人物横坐标对到待机图的（idle_anchor_x），
    # GroundToIdle 按可见底边对齐（同 visible_bottom）
    "scowling_face": {"scale": 0.30, "anchor": "visible_bottom", "idle_anchor_x": 256,
                      "idles": {"Default": "social_floor_liberation/scowling_face/default.png"},
                      "poses": {"Move": {"image": "social_floor_liberation/scowling_face/move.png", "anchor_x": 235},
                                "Damaged": {"image": "social_floor_liberation/scowling_face/damaged.png", "anchor_x": 444},
                                "Hit": {"image": "social_floor_liberation/scowling_face/hit.png", "anchor_x": 604}}},
    # BloodBatCreatureVisuals（异想体与语言层的血蝙蝠同一外观）：全部按 0.78
    "blood_bat": {"scale": 0.78, "anchor": "visible_bottom",
                  "idles": {"Default": "nosferatu/blood_bat_idle.png"},
                  "poses": {"Ranged": {"image": "nosferatu/blood_bat_ranged.png"},
                            "Attack": {"image": "nosferatu/blood_bat_attack.png"},
                            "Evade": {"image": "nosferatu/blood_bat_evade.png"}}},
    # TheFourthMatchFlameCreatureVisuals：Centered，布局 0.38；攻击是 Lunge、Scale(0.42)
    "fourth_match": {"scale": 0.38, "anchor": "center",
                     "idles": {"Default": "the_fourth_match_flame.png"},
                     "poses": {"Attack": {"image": "the_fourth_match_flame_attack.webp", "frame_scale": 0.42},
                               "Hit": {"image": "the_fourth_match_flame_hit.webp"}}},
    # 以下原来是场景动画。文学层强化小蜘蛛各姿势缩放不一（待机 0.55、攻击 0.4~0.43、施法 0.6、受击 0.45），
    # 照场景摆放，大小靠标注页的尺寸两端统一
    "lf_small_spider": {"scene": "literature_floor_enhanced_small_spider.tscn", "ref_library": "enhanced_small_spider",
                        "libraries": {"enhanced_small_spider": "literature_floor_enhanced_small_spider_animations.tres"},
                        "idles": {"Default": "enhanced_small_spider"},
                        "poses": {"Move": ["enhanced_small_spider", "Attack", 0],
                                  "Attack": ["enhanced_small_spider", "Attack", 1],
                                  "Cast": ["enhanced_small_spider", "Cast", 0],
                                  "Hit": ["enhanced_small_spider", "Hit", 0]}},
    # 审判鸟、逃亡鸟：动画库嵌在场景文件里
    "judgement_bird": {"scene": "judgement_bird.tscn", "ref_library": "default",
                       "libraries": {"default": "judgement_bird.tscn"},
                       "idles": {"Default": "default"},
                       "poses": {"Attack": ["default", "Attack", 0], "Guard": ["default", "Guard", 0],
                                 "Hit": ["default", "Hit", 0]}},
    "escaped_bird": {"scene": "escaped_bird.tscn", "ref_library": "default",
                     "libraries": {"default": "escaped_bird.tscn"},
                     "idles": {"Default": "default"},
                     "poses": {"AttackOne": ["default", "AttackOne", 0], "AttackTwo": ["default", "AttackTwo", 0],
                               "Hit": ["default", "Hit", 0]}},
    # ---- 第六批 ----
    # BigBadWolfCreatureVisuals：Centered，布局 0.72；普通、吞下两个形态，各有打击、斩击、受击
    "big_bad_wolf": {"scale": 0.72, "anchor": "center",
                     "idles": {"Normal": "big_bad_wolf/idle.png", "Swallowed": "big_bad_wolf/swallow.png"},
                     "poses": {"Strike": {"image": "big_bad_wolf/strike.png"}, "Slash": {"image": "big_bad_wolf/slash.png"},
                               "Hit": {"image": "big_bad_wolf/hit.png"},
                               "SwallowedStrike": {"image": "big_bad_wolf/swallowed_strike.png"},
                               "SwallowedSlash": {"image": "big_bad_wolf/swallowed_slash.png"},
                               "SwallowedHit": {"image": "big_bad_wolf/swallowed_hit.png"}}},
    # ScaredyCatCreatureVisuals：布局 0.88；归家的路途被打败后换成同伴形态（companion 两张）
    "scaredy_cat": {"scale": 0.88, "anchor": "visible_bottom",
                    "idles": {"Normal": "scaredy_cat/idle.png", "Companion": "scaredy_cat/companion_idle.png"},
                    "poses": {"Strike": {"image": "scaredy_cat/attack_strike.png"},
                              "Slash": {"image": "scaredy_cat/attack_slash.png"},
                              "Ranged": {"image": "scaredy_cat/attack_ranged.png"},
                              "Hit": {"image": "scaredy_cat/hit.png"},
                              "CompanionHit": {"image": "scaredy_cat/companion_hit.png"}}},
    # ScorchedGirlMonsterCreatureVisuals：Centered，布局 0.52；攻击是 Lunge、Scale(0.56)
    "scorched_girl": {"scale": 0.52, "anchor": "center",
                      "idles": {"Default": "scorched_girl_monster.png"},
                      "poses": {"Attack": {"image": "scorched_girl_monster_attack.webp", "frame_scale": 0.56},
                                "Hit": {"image": "scorched_girl_monster_hit.webp"}}},
    # OzmaCreatureVisuals：布局 0.48
    "ozma": {"scale": 0.48, "anchor": "visible_bottom",
             "idles": {"Default": "ozma/ozma_idle.png"},
             "poses": {f: {"image": f"ozma/ozma_{f.lower()}.png"} for f in ("Attack", "Guard", "Pain", "Sorrow", "Hit")}},
    # ---- 第七批：艺术层的雕像式人形 ----
    # ArtFloorFirstPerformerCreatureVisuals：Centered，布局 0.64，只有一张待机图
    "first_performer": {"scale": 0.64, "anchor": "center",
                        "idles": {"Default": "art_floor/first_performer.png"}, "poses": {}},
    # ArtFloorDaCapoPerformerCreatureVisuals：四种演奏者各一套（待机、攻击、防御、受击），全部按布局 0.48
    **{f"dacapo_performer_{i}": {"scale": 0.48, "anchor": "visible_bottom",
                                 "idles": {"Default": f"art_floor/dacapo_performers/performer_{i}_idle.png"},
                                 "poses": {p: {"image": f"art_floor/dacapo_performers/performer_{i}_{p.lower()}.png"}
                                           for p in ("Attack", "Guard", "Hit")}}
       for i in range(1, 5)},
    # ArtFloorDustbornPersonCreatureVisuals：布局 0.50
    "dustborn": {"scale": 0.50, "anchor": "visible_bottom",
                 "idles": {"Default": "art_floor/nostalgic_scent/dustborn_idle.png"},
                 "poses": {p: {"image": f"art_floor/nostalgic_scent/dustborn_{p.lower()}.png"}
                           for p in ("Pierce", "Slash", "Hit", "Dodge")}},
    # 以下原来是场景动画。噩梦中的狼：待机、嚎叫、受击 0.6 倍，斩击、突刺 0.48 倍（照场景，大小靠尺寸两端统一）
    "wolf_nightmare": {"scene": "wolf_in_her_nightmares.tscn", "ref_library": "wolf",
                       "libraries": {"wolf": "wolf_in_her_nightmares_animations.tres"},
                       "idles": {"Default": "wolf"},
                       "poses": {a: ["wolf", a, 0] for a in ("Slash", "Thrust", "Howl", "Hit")}},
    # 宗教层三位使徒：动画库嵌在场景里（normal、dead 同名动作，scene_poses 取后定义的 normal）；
    # 特殊招式按 0、1、2 秒依次换 s1、s2、s3
    **{f"{a}_apostle": {"scene": f"religion_floor_{a}_apostle.tscn", "ref_library": "normal",
                        "libraries": {"normal": f"religion_floor_{a}_apostle.tscn"},
                        "idles": {"Default": "normal"},
                        "poses": {**{p: ["normal", p, 0] for p in attacks},
                                  "Guard": ["normal", "Guard", 0], "Hit": ["normal", "Hit", 0],
                                  "S1": ["normal", "Special", 0], "S2": ["normal", "Special", 1],
                                  "S3": ["normal", "Special", 2]}}
       for a, attacks in (("scythe", ("Slash", "Strike")), ("spear", ("Pierce",)), ("staff", ("Attack",)))},
}


def visible_bottom(im):
    a = np.asarray(im.getchannel("A"))
    rows = np.nonzero((a > 0.03 * 255).any(axis=1))[0]
    return float(rows[-1] + 1) if len(rows) else float(im.height)


def scene_placements(spec):
    """原来是场景动画的怪物：按场景里每个动作那张贴图的 position / offset / scale 摆放（scene_poses.py 读出）。
    坐标单位是 ref 动画库待机贴图的像素；每个动画库的待机图定一副骨架的原点（它的矩形底边中点，AlignTo 对齐的点）。
    poses: {动作名: [动画库, 动画, 第几张]}；idles: {动作名: 动画库}（该库的 Idle）。"""
    from scene_poses import poses as read_scene  # noqa: PLC0415
    R = M.parents[1]
    tscn = R / "scenes/creature_visuals" / spec["scene"]
    libs = read_scene(tscn, [R / "scenes/creature_visuals" / f for f in spec["libraries"].values()])
    lib_of = {lib: libs[Path(f).stem] for lib, f in spec["libraries"].items()}
    ref_scale = lib_of[spec["ref_library"]]["Idle"][0]["scale"][0]
    # poses 的第 4 项（可选）是尺寸修正：原图里的人物和待机图同样大，场景却给了更小的缩放时放大回去
    entries = [(m, lib, "Idle", 0, True, 1.0) for m, lib in spec["idles"].items()]
    entries += [(m, v[0], v[1], v[2], False, v[3] if len(v) > 3 else 1.0) for m, v in spec["poses"].items()]
    for motion, lib, anim, i, idle, fix in entries:
        shot = lib_of[lib][anim][i]
        src = R / shot["texture"].replace("res://", "")
        im = Image.open(src).convert("RGBA")
        W0, H0 = im.size
        sc = shot["scale"][0]
        ox, oy = shot["offset"]
        # Sprite2D：不居中时贴图左上角在 offset，居中时贴图中心在 offset；整体再乘 scale、加 position
        tlx, tly = (ox, oy) if not shot["centered"] else (ox - W0 / 2, oy - H0 / 2)
        px, py = shot["position"]
        center = (px + (tlx + W0 / 2) * sc, py + (tly + H0 / 2) * sc)
        # library_fix：整个动画库的图都画小了（微笑的尸山二、三阶段里一阶段那团只有一半大），该库的待机和动作一起放大
        k = sc / ref_scale * fix * spec.get("library_fix", {}).get(lib, 1.0)
        origin = None
        if idle:
            bottom = (px + (tlx + W0 / 2) * sc, py + (tly + H0) * sc)
            origin = [round(bottom[0] / ref_scale, 1), round(-bottom[1] / ref_scale, 1)]
        if k != 1.0:
            im = im.resize((round(W0 * k), round(H0 * k)), Image.LANCZOS)
        yield {"motion": motion, "idle": idle, "im": im, "k": k, "src": src, "cx": center[0] / ref_scale,
               "cy": -center[1] / ref_scale, "origin": origin, "root_scale": round(ref_scale / sc, 4) if idle else None}


def sprite_placements(spec):
    """换图外观（SpriteAttackCreatureVisuals）的摆放规则，见文件开头。"""
    s = spec["scale"]
    poses = {**{k: {**(v if isinstance(v, dict) else {"image": v}), "idle": True} for k, v in spec["idles"].items()},
             **spec["poses"]}
    for motion, p in poses.items():
        im = Image.open(M / p["image"]).convert("RGBA")
        # 待机贴图另给了缩放（Variant.Scale(...).IdleOnly()）时，动作贴图仍按外观布局的缩放、位置画：attack_scale、attack_offset
        if p.get("idle"):
            k0 = p.get("frame_scale", s) / s
        else:
            k0 = p.get("frame_scale", spec.get("attack_scale", s)) / s
            off = spec.get("attack_offset", [0, 0])
            p = {**p, "nudge": [p.get("nudge", [0, 0])[0] + off[0], p.get("nudge", [0, 0])[1] + off[1]]}
        k = k0 * p.get("fix", 1.0)
        origin = None
        if p.get("idle"):
            # 骨架原点对齐的是游戏里没放大的那张待机贴图（RuntimeSpineBody.AlignTo），原点按它算
            raw = Image.open(M / p["image"]).convert("RGBA")
            if k0 != 1.0:
                raw = raw.resize((round(raw.width * k0), round(raw.height * k0)), Image.LANCZOS)
            rH = raw.height
            origin = ([0.0, round(-(rH - visible_bottom(raw)), 1)] if spec["anchor"] == "visible_bottom"
                      else [0.0, round(-rH / 2, 1)])
        if k != 1.0:
            im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
        nx, ny = p.get("nudge", [0, 0])
        W, H = im.size
        if spec["anchor"] == "visible_bottom":
            vb = visible_bottom(im)
            cx, cy = nx / s, -ny / s + (vb - H / 2)
        else:
            cx, cy = nx / s, -ny / s
        if "anchor_x" in p:
            # AnchorX：动作图的人物横坐标（原图像素）对到待机图的 idle_anchor_x；待机图本身仍居中摆放
            idle_w = Image.open(M / next(iter(spec["idles"].values()))).width
            cx += (spec["idle_anchor_x"] - idle_w / 2) - (p["anchor_x"] - Image.open(M / p["image"]).width / 2) * k
        yield {"motion": motion, "idle": bool(p.get("idle")), "im": im, "k": k, "src": M / p["image"], "cx": cx, "cy": cy,
               "origin": origin, "root_scale": None, "pin": p.get("pin")}


def build(name, spec):
    out = LAYERS / name
    out.mkdir(parents=True, exist_ok=True)
    info = {}
    origins = {}
    root_scales = {}
    size_ref = None
    pin_ref = None
    placed = list(scene_placements(spec) if "scene" in spec else sprite_placements(spec))
    ann_path = PINS / f"{name}.json"
    ann = json.loads(ann_path.read_text()) if ann_path.exists() else None
    if ann:
        mode, points = ann["mode"], ann["points"]
    else:
        points = {e["motion"]: e["pin"] for e in placed if e.get("pin")}
        mode = "pin" if points else ("pin_y" if spec.get("pin_y") else "none")
    for e in placed:
        motion, im, k, cx, cy = e["motion"], e["im"], e["k"], e["cx"], e["cy"]
        W, H = im.size
        if e["origin"] is not None:
            origins[motion] = e["origin"]
        if e["root_scale"] not in (None, 1.0):
            # 这个动画库的待机贴图与参照库缩放不同（自然层遗忘骑士之剑泪滴形态 1 倍、其余 0.5 倍）：
            # 骨架单位要等于它自己待机贴图的像素，整体乘 root_scale
            root_scales[motion] = e["root_scale"]
        # 尺寸标定（标注页的“尺寸两端”）：同一件东西在各张图里画得大小不一时，每张标两个端点（剑柄头到剑尖），
        # 以第一个标了的姿势为准，其余姿势绕图中心缩放到同样长
        # size_like：没标的姿势照搬构图相同的另一张图的线段（历史层剑的泪滴、普通打击图同尺寸同构图）
        sizes = (ann or {}).get("sizes", {})
        seg = sizes.get(motion) or sizes.get(spec.get("size_like", {}).get(motion))
        if seg:
            length = float(np.hypot(seg[1][0] - seg[0][0], seg[1][1] - seg[0][1])) * k
            if size_ref is None:
                size_ref = length
            elif length > 1:
                f = size_ref / length
                if abs(f - 1) > 0.005:
                    im = im.resize((max(1, round(W * f)), max(1, round(H * f))), Image.LANCZOS)
                    k *= f
                    W, H = im.size
        # 对齐点（原图像素）：第一个有点的姿势（待机图排在前面）定下参照点，其余姿势把自己的点对到这一点。
        # pin 横纵都对齐，pin_y 只对高度。点来自标注页（pins/<名字>.json），没有标注时用配置里的 pin，
        # 或 pin_y: dark_bottom（最低的暗色像素：大鸟的爪尖；可见底边可能是低垂的提灯光晕，不能用它对）
        pt = points.get(motion)
        if pt is not None and spec.get("points_flipped"):
            pt = [Image.open(e["src"]).width - pt[0], pt[1]]
        if pt is None and mode == "pin_y" and spec.get("pin_y") == "dark_bottom" and not ann:
            a = np.asarray(im).astype(np.float32)
            rows = np.nonzero(((a[..., 3] > 200) & (a[..., :3].max(-1) < 70)).any(axis=1))[0]
            pt = [W / 2 / k, (rows[-1] + 1) / k]
        # “不钉”时标的点不起作用（用户定的：不钉就保持原样）
        if pt is not None and mode in ("pin", "pin_y"):
            px, py = pt[0] * k - W / 2, pt[1] * k - H / 2
            if pin_ref is None:
                pin_ref = (cx + px, cy - py)
            elif mode == "pin":
                cx, cy = pin_ref[0] - px, pin_ref[1] + py
            else:
                cy = pin_ref[1] + py
        (out / motion).mkdir(exist_ok=True)
        layers = []
        body = im.copy()
        for i, c in enumerate(spec.get("cutouts", {}).get(motion, [])):
            ex, ey, rx, ry = c["ellipse"]
            arr = np.asarray(im).astype(np.float32)
            yy, xx = np.mgrid[0:H, 0:W]
            inside = ((xx - ex) / rx) ** 2 + ((yy - ey) / ry) ** 2 <= 1
            lum = arr[..., :3].max(-1)
            sat = lum - arr[..., :3].min(-1)
            if "away" in c:
                # 长在高饱和身体旁边的灰白薄片（蜂后的翅膀）：椭圆里离身体（饱和度 ≥ sat 的不透明像素）
                # 超过 away 像素的都算部件，连同翅膀的半透明填充、脉络和黑色描边；身体自己的描边贴着黄红色，留在身体上
                from scipy import ndimage
                solid = (sat >= c.get("sat", 80)) & (arr[..., 3] > 200)
                near = ndimage.binary_dilation(solid, iterations=c["away"])
                take = inside & (arr[..., 3] > 0) & ~near
            else:
                # 椭圆里够亮的像素从身体上去掉；box（部件本体，含暗色框线）整块去掉
                take = inside & (lum >= c["min_lum"]) & (arr[..., 3] > 0)
            box = np.zeros_like(take)
            if "box" in c:
                bx0, by0, bx1, by1 = c["box"]
                # box 里只取部件本身（有色或暗的像素），不取淡色光晕，否则光晕重画后会叠出一块方形
                box = ((xx >= bx0) & (xx <= bx1) & (yy >= by0) & (yy <= by1) & (arr[..., 3] > 0)
                       & ((sat >= 80) | (lum < 200)))
            part = arr.copy()
            part[..., 3] = np.where(take | box, arr[..., 3], 0)
            # 多个部件时身体要累计挖掉每一块
            rest = np.asarray(body).astype(np.float32)
            rest[..., 3] = np.where(take | box, 0, rest[..., 3])
            body = Image.fromarray(rest.astype(np.uint8), "RGBA")
            part_im = Image.fromarray(part.astype(np.uint8), "RGBA")
            if "glow" in c:
                g = c["glow"]
                t = np.hypot(xx - g["center"][0], yy - g["center"][1]) / g["radius"]
                a = g["alpha"] * np.clip((1 - t) / (1 - g["core"]), 0, 1) ** 1.6
                mix = np.clip(t, 0, 1)[..., None]
                rgb = np.array(g["inner"]) * (1 - mix) + np.array(g["outer"]) * mix
                glow = Image.fromarray(np.dstack([rgb, a]).astype(np.uint8), "RGBA")
                only_box = part.copy()
                only_box[..., 3] = np.where(box, arr[..., 3], 0)
                glow.alpha_composite(Image.fromarray(only_box.astype(np.uint8), "RGBA"))
                part_im = glow
            part_im.save(out / motion / f"{c['name']}.png")
            # behind：部件画在身体后面（翅膀根部藏在身体后，扇动时不露出断口）
            layers.append({"name": c["name"], "type": 9, "order": (-10 + i) if c.get("behind") else 10 + i,
                           "center": [cx, cy]})
        body.save(out / motion / "body.png")
        layers.insert(0, {"name": "body", "type": 2, "order": 1, "center": [cx, cy]})
        for l in layers:
            l.update({"enabled": True, "active": True, "pos": l["center"], "rot": 0.0, "scale": [1.0, 1.0],
                      "size": [W, H], "pivot": [0.5, 0.5], "ppu": 100.0, "motion": motion})
            l["center"] = [round(v, 2) for v in l["center"]]
        info[motion] = {"layers": layers, "composite_origin": [round(W / 2 - cx, 2), round(cy + H / 2, 2)]}
        im.save(out / f"{motion}.composite.png")
    (out / "layers.json").write_text(json.dumps(info, ensure_ascii=False, indent=1))
    # 参照点在骨架坐标里的位置：标注勾了“当转轴”时 build_boss_configs 把身体转轴放在这里；
    # 各待机的原点与 root_scale 供配置写 "origin": "auto"
    (out / "pin_ref.json").write_text(json.dumps({"mode": mode, "ref": [round(float(v), 2) for v in pin_ref] if pin_ref else None,
                                                  "origins": origins, "root_scales": root_scales,
                                                  **({"pivot": ann["pivot"], "noTilt": ann["noTilt"]} if ann else {})}))
    print(name, "origins", json.dumps(origins), "root_scales", root_scales, "mode", mode,
          "ref", pin_ref and [round(float(v), 1) for v in pin_ref])


def main():
    only = set(sys.argv[1:])
    for name, spec in SPRITES.items():
        if not only or name in only:
            build(name, spec)


if __name__ == "__main__":
    main()
