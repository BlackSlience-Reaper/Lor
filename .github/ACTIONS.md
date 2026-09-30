# GitHub Actions

从 `BlackSlience-Reaper/LibraryOfRuina` 的 `1d6ce221` 同步三项轻量检查及已有 Qodana 配置，并纳入旧项目工作区的 Monster HP 检查。玩法变更来源为 `631b1c4d`（v0.22.2），按本项目 `bfe74b46` 的 `src/content/` 目录迁入。

## 自动检查

以下四个工作流在 `push`、`pull_request` 和 `workflow_dispatch` 时运行，权限均为 `contents: read`，每项限时 10 分钟，不要求安装游戏或配置 secrets。

- `localization.yml`：读取 `LibraryOfRuina/localization/` 的 zhs/eng/jpn/kor JSON（含 settings_ui），检查 UTF-8、JSON 语法、重复键和字符串值；不检查译文、跨语言键集合或动态占位符语义。
- `manifest.yml`：检查 manifest 的 ID 与 `AssemblyName` 一致、版本格式、DLL/PCK 标志、依赖 ID/版本格式和已跟踪 csproj XML。本项目通过 `RitsuLibReferencesProps` 引用已安装的 RitsuLib；检查对应 Import 存在。安装版本与游戏 DLL 仍由本机构建核对。
- `powershell.yml`：用 PowerShell Parser 解析已跟踪的脚本，排除第三方 node_modules，不执行部署、下载或素材复制。
- `monster-hp.yml`：用独立 .NET 10 / Roslyn 工具分别解析 Beta 和 Public 分支，检查具体怪物的 `MinInitialHp <= MaxInitialHp`；先运行工具自带回归用例，再扫描源码。工具说明见 `tools/MonsterHpCheck/README.md`。

`_copy_assets.ps1` 保留旧项目一个源素材复制到多个目标的修复。源目录改为必填 `-SourceDirectory`，目标目录默认当前仓库，避免使用旧项目的机器路径。CI 只检查该脚本语法。

## Qodana

`qodana_code_quality.yml` 保留为手动入口。目标仓库需要配置自己的 `QODANA_TOKEN`；缺少时在摘要中说明跳过。工作流不发布 PR 评论或自动修复。本项目的游戏及前置 DLL 尚未配置到托管 runner，Qodana 的完整语义分析需要另行提供这些构建依赖。

## 本地执行

```powershell
python .github/scripts/check_metadata.py localization
python .github/scripts/check_metadata.py manifest
pwsh -NoProfile -File .github/scripts/check_powershell.ps1
Push-Location tools/MonsterHpCheck
dotnet run --configuration Release -- --self-test
dotnet run --configuration Release --no-build -- ../..
Pop-Location
```

这些检查的成功只证明各自覆盖的静态约束。游戏内战斗、意图显示、存档和联机结果仍需实际运行验证。
