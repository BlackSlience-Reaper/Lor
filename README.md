# Library Of Ruina — Slay the Spire 2 Mod

《杀戮尖塔2》内容扩展模组，加入《废墟图书馆》风格的怪物、遭遇战、Boss、事件、书页遗物、E.G.O. 卡牌、能力、详细意图显示，以及考虑联机的平衡支持。

A Slay the Spire 2 content expansion that adds Library of Ruina-inspired monsters, encounters, bosses, events, page relics, E.G.O. cards, powers, detailed intents and multiplayer-aware balance.

- 作者 / Author: **ShuiMuNianHua**
- 版本 / Version: v0.21.2（游戏版本 ≥ 0.111.0）
- 许可 / License: [MIT](LICENSE)（仅覆盖本模组自己的代码与工程文件，第三方素材见下文）

## 前置模组 / Dependencies

| 模组 | 最低版本 | 地址 |
| --- | --- | --- |
| LibraryOfRuinaLib（废墟图书馆基础库） | 1.2.14 | https://github.com/Xuyuha/LibraryOfRuinaLib |
| STS2-RitsuLib | 0.6.2 | https://github.com/BAKAOLC/STS2-RitsuLib |
| ActLikeIt2 | 0.2.1 | https://github.com/Darkglade1/ActLikeIt2 |

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

导出时 Godot 会编译 C# 工程，所以也需要先配置好第 1 步的路径。

### 4. 安装

```
mods/LibraryOfRuina/
├── LibraryOfRuina.dll
├── LibraryOfRuina.json
└── LibraryOfRuina.pck
```

## 目录 / Layout

| 目录 | 内容 |
| --- | --- |
| `src/` | 全部 C# 源码（模型、Hook、补丁、UI、联机兼容等） |
| `scenes/` `materials/` `shaders/` | Godot 场景、材质与着色器 |
| `images/` `audio/` `videos/` `fonts/` | 美术、音频、视频与字体素材（Git LFS） |
| `localization/` `LibraryOfRuina/localization/` | 本地化文本 |
| `addons/mega_text/` | 游戏自带的 MegaLabel 控件，供场景在编辑器中打开 |

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
- `fonts/` 下的字体：Noto Sans CJK、Nanum 系列（SIL Open Font License 1.1），Arita-buri（© AMOREPACIFIC）。

## Git LFS

图片、音频、视频和字体通过 Git LFS 存储。克隆前请先安装 Git LFS：

```bash
git lfs install
```
