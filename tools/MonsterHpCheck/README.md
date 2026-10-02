# 怪物初始 HP 检查

`.github/workflows/monster-hp.yml` 在所有分支的 `push`、`pull_request` 和手动 `workflow_dispatch` 时运行，权限为 `contents: read`，限时 10 分钟。

检查器使用独立的 .NET 10 控制台项目及 SDK 自带的 Roslyn，读取 `src/**/*.cs`，按主项目排除 `src/debug` 与 `src/encounters/debug`。无需游戏、Godot、相邻基础库或 secrets。按 `Directory.Build.props` 的两个兼容目标分别解析（定义 `STS2_0_107_1` 或 `STS2_0_111_0`，与 `-p:CompatibilityTarget` 编译时相同），自动识别怪物及继承链，逐个校验：

- `ascensionValue` 对应的 `MinInitialHp <= MaxInitialHp`。
- `fallbackValue` 对应的 `MinInitialHp <= MaxInitialHp`。
- 固定 HP、条件分支和阶段分支也满足相同的上下限关系，允许上下限相等。

支持数值与常量表达式、命名参数、`HpValue` 等源码包装函数、继承/partial 类、属性引用、`ModelDb.Monster<T>()` HP 引用及整数比例计算。包装函数的参数含义依据实际函数体解析。两种进阶分支分别比较，允许高进阶 HP 小于普通 HP。无法解析的表达式、缺少 HP 定义或空扫描会使检查失败；同一动态字段作为上下限时按相等处理。工具只解析源码，不执行怪物代码。

失败以退出码 `1` 阻断该 CI job，并在 GitHub 标注源码位置、怪物、分支和 HP 数值；结果数量写入运行摘要。工作流运行检查器回归用例后再扫描全仓库。PR 合并的强制要求由仓库 ruleset/branch protection 的 required status checks 控制。

## 本地执行

需要 .NET 10 SDK；`global.json` 的版本配置仅作用于检查工具目录。从仓库根目录运行：

```powershell
Push-Location tools/MonsterHpCheck
dotnet run --configuration Release -- --self-test
dotnet run --configuration Release --no-build -- ../..
Pop-Location
```

`HpChecker.cs` 负责发现与分析，`SelfTests.cs` 覆盖两种进阶分支填反、常量、包装函数、继承、命名参数、动态相等、阶段、比例计算和无法解析时失败的行为。新增 HP 写法若检查失败，应补充解析与对应回归用例。
