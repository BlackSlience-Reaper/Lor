# Library Of Ruina — Slay the Spire 2 Mod

《杀戮尖塔2》内容扩展模组，加入《废墟图书馆》风格的怪物、遭遇战、Boss、事件、书页遗物、E.G.O. 卡牌、能力、详细意图显示，以及考虑联机的平衡支持。

A Slay the Spire 2 content expansion that adds Library of Ruina-inspired monsters, encounters, bosses, events, page relics, E.G.O. cards, powers, detailed intents and multiplayer-aware balance.

- 作者 / Author: **Natsuki, Reaper**
- 版本 / Version: v0.22.6（游戏版本 ≥ 0.111.0）
- 许可 / License: [MIT](LICENSE)（仅覆盖本模组自己的代码与工程文件，第三方素材见下文）

## 前置模组 / Dependencies

| 模组 | 最低版本 | 地址 |
| --- | --- | --- |
| LibraryOfRuinaLib（废墟图书馆基础库） | 1.3.0 | https://github.com/Xuyuha/LibraryOfRuinaLib |
| STS2-RitsuLib | 0.6.2 | https://github.com/BAKAOLC/STS2-RitsuLib |
| ActLikeIt2 | 0.2.2 | https://github.com/BlackSlience-Reaper/ActLikeIt2 |

## 使用说明 / Notes

**主界面音乐**：启用本模组后，主界面默认改为播放本模组的 BGM，替换原版主界面音乐。想听原版音乐时，在设置里「废墟图书馆」一行点「模组设置」，进入「音乐与音效」，先勾选「展开音乐与音效设置」，再关闭「启用自定义主界面BGM」。关闭「启用废墟图书馆内容」也会恢复原版音乐。

**Main menu music**: with this mod enabled, the main menu plays the mod's own BGM instead of the vanilla track by default. To keep the vanilla music, open Settings, click "Mod Configuration" on the "Library of Ruina" row, go to "Music and Sound Effects", turn on "Expand audio settings", then turn off "Enable custom main menu BGM". Turning off "Enable Library of Ruina content" also restores the vanilla music.

**调试日志**：逐张牌、逐次伤害之类的详细日志属于 Debug 级别，默认不输出。需要时用启动参数 `-log Generic Debug`，或在开发者控制台输入 `log debug`（这会同时打开原版的 Debug 日志）。

**Debug logging**: verbose per-card and per-hit logs are at Debug level and are off by default. Enable them with the launch option `-log Generic Debug`, or type `log debug` in the developer console (this also enables the game's own Debug logs).

## 构建 / Building

需要 .NET 9 SDK 和 Godot 4.5.1 .NET 版。

### 1. 配置路径

`Directory.Build.props` 按 Steam 默认安装位置推断游戏、RitsuLib（创意工坊 3747602295）以及 `mods/` 目录下的 ActLikeIt2、LibraryOfRuinaLib。路径不同时复制 `local.props.example` 为 `local.props` 并改写对应属性：

| 属性 | 含义 |
| --- | --- |
| `Sts2Dir` / `Sts2DataDir` | 游戏目录 / 含 `sts2.dll`、`0Harmony.dll` 的数据目录 |
| `RitsuLibRoot` | 含 `RitsuLib.References.props` 的 RitsuLib 安装目录 |
| `RitsuLibReferenceTarget` | RitsuLib `compat/` 下的游戏 API 版本，默认 `0.111.0` |
| `ActLikeIt2Dll` | `ActLikeIt2.dll` 路径 |
| `LibraryOfRuinaLibDll` | `LibraryOfRuinaLib.dll` 路径 |

基础库按 GitHub 源码编译：`tools/build_lib.sh` 把 https://github.com/Xuyuha/LibraryOfRuinaLib 检出到 `build/LibraryOfRuinaLib` 并编译（上游仓库不提交工程文件，脚本用 `tools/LibraryOfRuinaLib.csproj.template`），之后 `Directory.Build.props` 自动引用它的输出。默认检出脚本里的 `RELEASE_REF`，即 `LibraryOfRuina.json` 要求的已发布版本（现在是 1.3.0）对应的提交，提高最低版本时两处一起改；`tools/build_lib.sh origin/main` 可以试基础库的最新源码。没有运行脚本时回退到 `mods/` 下安装的基础库。玩家装的是基础库作者发布的版本，所以 `check.sh` 找到工坊里已发布的基础库时，还会用它再编译一次本模组。

### 2. 编译 DLL

```bash
dotnet build LibraryOfRuina.csproj -c Release
```

输出位于 `.godot/mono/temp/bin/Release/LibraryOfRuina.dll`。

工程默认定义 `STS2_BETA`，对应 v0.21.2 发布版所用的游戏 0.111 API。旧 API（`SavedPropertiesTypeCache`）用 `-p:Sts2Beta=false` 编译。

### 3. 导出 PCK

用 Godot 4.5.1 .NET 打开本目录并等待首次导入完成，然后用自带的 `LibraryOfRuina` 预设导出 PCK，或者直接在命令行执行：

```bash
godot --headless --path . --export-pack LibraryOfRuina build/LibraryOfRuina.pck
```

导出时 Godot 会编译 C# 工程，所以也需要先配置好第 1 步的路径。PCK 里的 `.cs` 只保留空占位（场景按路径引用脚本），不附带源码；`docs/`、`tools/`、`snapshots/`、`verification/` 带有 `.gdignore`，不会进包。

纹理用最高等级的无损 WebP 压缩（`project.godot` 的 `rendering/textures/webp_compression`）。修改这两项不会触发重新导入：已有的导入缓存要先删掉 `.godot/imported/` 再导入，否则导出的仍是旧的纹理。

### 4. 安装

```
mods/LibraryOfRuina/
├── LibraryOfRuina.dll
├── LibraryOfRuina.json
└── LibraryOfRuina.pck
```

### 5. 验证套件（开发用，可选）

`verification/` 是独立的验证模组 `LibraryOfRuinaVerification`，只在跑 headless 验证的机器上部署，不随发行包分发：

```bash
dotnet build verification/LibraryOfRuinaVerification.csproj -c Release
```

把 `verification/bin/Release/` 下的 `LibraryOfRuinaVerification.dll` 和 `LibraryOfRuinaVerification.json` 放进 `mods/LibraryOfRuinaVerification/`，再用 `--lor-verify-<套件>` 启动游戏，例如 `--headless --lor-verify-king-greed-summon-king`。套件通过后以退出码 0 退出，失败时退出码为 1。比对仓库素材的套件需要环境变量 `LOR_PROJECT_ROOT`（仓库根目录）；Laetitia 原图哈希检查另需 `LOR_ART_SOURCE_ROOT`，未设置时跳过。`--lor-verify-font-screenshots` 是界面截图场景，要用窗口模式（不加 `--headless`）运行，截图和 `manifest.json` 写到环境变量 `LOR_FONT_SHOT_DIR` 指定的目录，用法见套件源码开头的说明。

`multifight`、`multievent`、`tempmap` 控制台命令也在验证模组里。正式模组只保留 `lor_skip`（原名 `skip`），用于强制结算卡住的战斗或事件。

### 6. 重构护栏

`tools/check.sh` 会检查规范模型 getter，编译主工程和验证工程，再把模型 ID、SavedProperty、补丁清单、静态字段的快照与 `snapshots/` 比对。输出为空表示没有身份变化；有意变更时用 `tools/check.sh --accept` 更新基线。会跳过原方法的前缀（返回 bool）必须在类上写 `[LibraryPatch(Reason = "…")]`，说明原版为什么没有可用的 Hook 或虚方法、以及只作用于哪些内容；缺理由时 `check.sh` 直接失败。挂在原版 `Hook.*` 上的补丁同样要写理由，说明为什么不能由已有模型覆写对应的钩子方法。哪些类算补丁类由 `src/infra/patching/PatchClassRules.cs` 判定，安装器和快照工具共用；`tools/PatchRuleFixtures` 是它的测试，也由 `check.sh` 运行。运行期访问原版非公开成员只能经 `src/interop/VanillaPrivate.cs` 的访问器，`tools/PrivateAccessCheck`（按语法树）检查其余地方不按名字反射，不论成员名是字面量、常量还是变量（例外按“文件、所属成员、API”写在 `tools/private_access_allowlist.txt`，要写理由；`fixtures/` 是它的回归测试）；启动时初始化汇总会列出游戏更新后找不到的成员。

补丁由 `src/infra/patching/LibraryPatcher` 统一安装。主菜单第一次就绪时，它会在日志里报告与其他模组共享的目标，并点名排在本模组跳过型前缀之后的第三方前缀。

`src/infra/patching/vanilla_copy_guard.txt` 冻结了本模组用跳过型前缀或 Transpiler 修补的游戏与前置库方法的 IL 哈希（async 方法连同状态机）。游戏更新后，如果这些方法变了，日志会出现 `[LibraryOfRuina.VanillaCopyGuard] DRIFT`，需要逐个复查对应补丁。重新生成守卫表的方法：用环境变量 `LOR_DUMP_PATCHES=<目录>` 启动游戏，进到主菜单后退出，再把导出的 `vanilla_copy_guard.txt` 复制过来。同一目录下的 `patch_table.txt` 是实际安装的完整补丁表，包含同目标的执行顺序和其他模组的补丁，基线存放在 `snapshots/headless/`，重构补丁层时拿来前后比对。这两份都只能在装好本模组和前置的游戏里生成，`check.sh` 不会重新生成它们。

## 目录 / Layout

| 目录 | 内容 |
| --- | --- |
| `src/` | 全部 C# 源码（模型、Hook、补丁、UI、联机兼容等） |
| `scenes/` `materials/` `shaders/` | Godot 场景、材质与着色器 |
| `images/` `audio/` `videos/` `fonts/` | 美术、音频、视频与字体素材（Git LFS） |
| `localization/` `LibraryOfRuina/localization/` | 本地化文本 |
| `addons/mega_text/` | 游戏自带的 MegaLabel 控件，供场景在编辑器中打开 |
| `verification/` | headless 验证套件（独立模组，不进发行包） |
| `docs/` | 设计哲学、本地化规范、重构指导 |
| `tools/` `snapshots/` | 重构护栏脚本与身份快照基线 |

`src/` 按纵切组织，命名空间与目录一致（前缀都是 `LibraryOfRuina`）：

| 目录 | 内容 |
| --- | --- |
| `core/` | 初始化入口、`compat/` 版本兼容、`networking/`、`localization/`、`settings/` 设置与设置界面 |
| `infra/` | `patching/` 补丁安装与守卫、`helpers/` 类型发现等通用工具、`hooks/` 局级监听模型 |
| `framework/` | 跨内容共用的基类与组件：`monsters/` `encounters/` `cards/` `powers/` `relics/` `intents/` `visuals/` `audio/` `combat/` |
| `content/` | 一个实体一个文件夹：`abnormalities/<异想体>/`、`liberation/<楼层>/`、`guests/<事务所>/`、`specialguests/<来宾>/`，以及 `events/` `relics/` `acts/` `reverberation/` `afflictions/` |
| `patches/` | 不属于单个实体的补丁，`dispatch/` 是每个原版目标唯一的分派补丁 |
| `ui/` `features/` | 界面与独立功能（意图图、教程、临时地图等） |
| `interop/` | 给其他模组用的公开接口；命名空间是对外契约，不随目录调整 |

移动文件或改命名空间不能改类名（类名就是模型 ID）。补丁安装、卡池登记、盟友 provider 与事件遗物池按
`src/infra/helpers/type_discovery_order.txt` 的顺序发现，新增这几类类型时照表头说明登记。

## 关于本仓库的来源

本仓库由 v0.21.2 发布包里的 `LibraryOfRuina.pck` 还原而来。PCK 中已包含全部 C# 源码和文本场景，DLL 中的全部类型都能在 `src/` 找到对应源码。导出过程只留下了导入后的产物，以下内容经过还原或重建：

- **图片**：从 `.ctex` 中取出原始 WebP 数据，逐像素无损转回 PNG/WebP。原本就是有损压缩的纹理在 `.import` 中保留 `compress/mode=1`。
- **音频**：从 `.oggvorbisstr` 中取出 Vorbis 数据包，重新封装为 `.ogg`，音频数据未重新编码；循环参数从资源本身读回。
- **字体**：从导入后的 FontFile 中取回原始字体文件。
- **`.import`**：导出时只保留了 `[remap]` 段，`[params]` 按资源实际属性重建（压缩模式、mipmap、循环、字体选项），其余导入选项为 Godot 默认值。
- **重新编写的文件**：`project.godot`（由 `project.binary` 转写）、`LibraryOfRuina.csproj`、`LibraryOfRuina.sln`、`Directory.Build.props`、`export_presets.cfg`、`addons/mega_text/plugin.gd`（由编译后的 `.gdc` 还原）与 `plugin.cfg`。
- **`.uid`**：脚本与着色器的 UID 从导出包的 `uid_cache.bin` 写回，场景中的 UID 引用保持有效。

还原结果的核对方式：用 Godot 重新导入本工程，795 个音频、9 个字体和 3254 张纹理的导入产物与原 PCK 逐字节一致，另有 370 张纹理逐像素一致；2 张 DXT5 纹理因 Godot 重新做有损压缩而略有差异。用本仓库导出的 PCK 与原版文件清单完全一致。编译出的 DLL 与发布版的全部类型一一对应，其余差异只来自编译器版本的代码生成方式。

## 第三方内容 / Third-party content

MIT 许可只适用于本模组自己编写的代码与工程文件。以下内容不在其授权范围内，版权归各自权利人：

- 取自或基于《废墟图书馆》《脑叶公司》等 Project Moon 作品的美术、动画、音频、音乐、视频、角色与剧情文本 © Project Moon，仅限非商业同人用途。
- `addons/mega_text/` 属于《杀戮尖塔2》© Mega Crit Games。
- `fonts/` 下的字体：Noto Sans CJK SC（子集，由 `tools/subset_zhs_font.py` 生成）、Nanum Barun Gothic（SIL Open Font License 1.1），Arita-buri（© AMOREPACIFIC）。日文剧情字体直接使用游戏自带的 Noto Sans CJK JP。

## Git LFS

图片、音频、视频和字体通过 Git LFS 存储。克隆前请先安装 Git LFS：

```bash
git lfs install
```
