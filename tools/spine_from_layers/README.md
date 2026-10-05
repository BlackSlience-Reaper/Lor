# spine_from_layers

用原版《废墟图书馆》的分层立绘给来宾生成 Spine 骨骼（`images/monsters/guest_*.atlas / .spine-json / .webp`）。

原版人物在 `LibraryOfRuina_Data/StreamingAssets/AssetBundles/char/char_N.pres`（UnityFS，UnityPy 可读）。来宾的
`appearance_<名字>` 预制体里每个动作（Default、Hit、Penetrate、Slash、Damaged……）是一组 SpriteRenderer：身体、头、脸、
前发、皮肤层（白底贴图，游戏运行时乘肤色）等。原版游戏文件不进仓库，脚本默认从 `~/.local/share/LibraryOfRuina-win`
读，分层导出到 `~/.local/share/LibraryOfRuina-layers`。

流程：

1. `export_guests.sh`：按清单导出各来宾的分层（`export_layers.py`），每个动作一个目录加合成图与 `layers.json`。
2. `match_all.py` → `merge_match.py` → `skin_tint.py`：把模组现有的待机、攻击、受击图对上原版动作，得到骨架原点
   （模组待机图底边中点在原版坐标里的位置，游戏里骨架按它对齐）和肤色，写进 `match.json`。
3. `build_guest_configs.py`：按统一规则生成 `guest_<名字>.json`（艾莉的配置是手调的，带手臂骨，不要重新生成覆盖）。
4. `spine_from_layers.py <config>`：生成骨骼与图集。
5. `render_guests.py`：用游戏引擎渲染预览并拼 21 人总览 GIF；`review_sheet.py`、`layer_sheet.py`、`grid_motion.py`
   用来抽关键帧、看各层、读转轴坐标。

Boss（触发多、每个触发一张原图）走另一条线：`build_boss_configs.py` 里手填原版动作对应、停留时长、武器转轴
（E.G.O 司书没有单独的手部层，自动找不到握把），`type_tints.py` 求骨架原点并核对各类层要不要上色，
`render_bosses.py` 渲染并拼多格 GIF，`review_boss.py` 抽关键帧。产物是 `boss_<名字>.json` → `images/monsters/<楼层>/boss_*`。

查哪些怪物在原版有分层：`survey_bundles.py` 盘点每个预制体的动作数与层数，`signatures.py` 用缩略特征把模组贴图对到
原版动作，`name_map.py` 把贴图路径对回怪物类与中文名。

换姿势是附件瞬间切换，不做逐层淡入淡出：Spine 不能把一组层合起来再淡化，逐层半透明会透出被前发盖住的光头皮。
