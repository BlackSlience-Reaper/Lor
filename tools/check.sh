#!/bin/bash
# Build Debug and Release, then diff a fresh snapshot against the committed baseline.
# usage: tools/check.sh            — prints the snapshot diff (empty = no identity change)
#        tools/check.sh --accept   — overwrite snapshots/ with the current state
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export CompatibilityTarget="${CompatibilityTarget:-0.111.0}"
BASELINE="$ROOT/snapshots/$CompatibilityTarget"
PROJECT="$ROOT/LibraryOfRuina.csproj"
TMP="$(mktemp -d)"
FIXTURE_OUT="$(mktemp -d)"
trap 'rm -rf "$TMP" "$FIXTURE_OUT"' EXIT

python3 "$ROOT/tools/check_canonical_getters.py" "$ROOT/src"
# 原版把 DebugOnlyGetState 标为仅测试用；src/ 只能经 infra/lifecycle/CurrentState.cs 的 CurrentRun/CurrentCombat 取当前局和战斗。
if grep -rn --include='*.cs' 'DebugOnlyGetState' "$ROOT/src" | grep -v '/src/infra/lifecycle/CurrentState\.cs:'; then
  echo "DebugOnlyGetState outside src/infra/lifecycle/CurrentState.cs; use CurrentRun/CurrentCombat or the context's RunState/CombatState" >&2
  exit 1
fi
# The zhs story font is a subset; new zhs text must not use a character outside it.
python3 "$ROOT/tools/check_zhs_font.py"
# src/ 里完整的 res:// 字面量必须指向仓库里的文件，或列在原版/前置库/可选路径清单里（大小写敏感）。
python3 "$ROOT/tools/check_res_paths.py"
# No reflection by name outside src/interop/ (syntax-based); the self-test covers the forms that must be caught.
dotnet run --project "$ROOT/tools/PrivateAccessCheck/PrivateAccessCheck.csproj" -c Release -- --self-test "$ROOT/tools/PrivateAccessCheck/fixtures"
dotnet run --project "$ROOT/tools/PrivateAccessCheck/PrivateAccessCheck.csproj" -c Release --no-build -- "$ROOT/src" "$ROOT/tools/private_access_allowlist.txt"
# Players run the library author's release, not our source build: compile once against the published DLL too, so
# the mod cannot depend on library APIs that are not released yet. The normal build below then restores the output.
PUBLISHED_ROOT="$(dotnet msbuild "$PROJECT" -nologo -getProperty:SteamRoot)/steamapps/workshop/content/2868840/3747541096"
PUBLISHED_LIB="$PUBLISHED_ROOT/lib/$CompatibilityTarget/LibraryOfRuinaLib.dll"
if [[ ! -f "$PUBLISHED_LIB" && "$CompatibilityTarget" == "0.111.0" ]]; then
  PUBLISHED_LIB="$PUBLISHED_ROOT/LibraryOfRuinaLib.dll"
fi
if [[ -f "$PUBLISHED_LIB" ]]; then
  dotnet build "$PROJECT" -c Release -nologo -v q -clp:ErrorsOnly -p:LibraryOfRuinaLibDll="$PUBLISHED_LIB"
else
  echo "当前目标无已发布兼容库：使用本地双版本 LibraryOfRuinaLib 进行检查" >&2
fi
dotnet build "$PROJECT" -c Release -nologo -v q -clp:ErrorsOnly
# The verification suites reach into internals; build them too so they do not silently rot.
dotnet build "$ROOT/verification/LibraryOfRuinaVerification.csproj" -c Release -nologo -v q -clp:ErrorsOnly
# 玩家网络 ID → 整数映射的字符串是存档格式：PlayerIntMapSerializer 必须与合并前的四份实现逐字节一致。
dotnet run --project "$ROOT/tools/PlayerIntMapCheck/PlayerIntMapCheck.csproj" -c Release
"$ROOT/tools/snapshot.sh" "$TMP" Debug >/dev/null
if grep -q '[^[:space:]]' "$TMP/unresolved.txt"; then
  echo "元数据快照存在未解析类型：" >&2
  cat "$TMP/unresolved.txt" >&2
  exit 1
fi

# 补丁目标：两个目标都用各自的 Release 产物和引用检查（当前目标之外的那个在这里补编一次），不能只看本次编译的目标。
# - PatchTargetCheck：在独立 AssemblyLoadContext 里执行全部补丁类的 Prepare/TargetMethod(s)，由该目标的 Harmony 解析
#   静态特性，再对确定的原方法核对参数注入；异常、解析为空、Prepare 拒绝都算失败，例外只能登记在 exceptions.txt。
# - DualTargetAudit：只读元数据，按 Harmony 2.4.2 规则（合并方法级特性、只看声明类型、无参数表遇重载即歧义）解析静态目标。
# - GuardSnapshot：守卫范围取自实际解析出的目标，与运行时共用 IL 指纹算法；接受快照盖不住守卫表的差异。
# 先跑回归用例，确认每类错误仍会被报出。
PTC_FIXTURES="$ROOT/tools/PatchTargetCheck/Fixtures"
dotnet build "$PTC_FIXTURES/PatchTargetFixtures.csproj" -c Release -nologo -v q -clp:ErrorsOnly
for tool in PatchTargetCheck GuardSnapshot DualTargetAudit; do
  dotnet build "$ROOT/tools/$tool/$tool.csproj" -c Release -nologo -v q -clp:ErrorsOnly
done
PTC="$ROOT/tools/PatchTargetCheck/bin/Release/net9.0/PatchTargetCheck.dll"
GUARD_TOOL="$ROOT/tools/GuardSnapshot/bin/Release/net9.0/GuardSnapshot.dll"
AUDIT="$ROOT/tools/DualTargetAudit/bin/Release/net9.0/DualTargetAudit.dll"
FX_BIN="$PTC_FIXTURES/bin/Release/net9.0"
printf '# 回归用例不登记例外\n' > "$FIXTURE_OUT/no_exceptions.txt"
dotnet "$PTC" fixture "$FX_BIN/PatchTargetFixtures.dll" "$FIXTURE_OUT/no_exceptions.txt" "$FIXTURE_OUT/ptc_fixture.txt" "$FX_BIN" >/dev/null 2>&1 || true
diff -u "$PTC_FIXTURES/expected_dynamic.txt" <(grep -v '^#' "$FIXTURE_OUT/ptc_fixture.txt")
dotnet "$AUDIT" "$FX_BIN/PatchTargetFixtures.dll" "$FIXTURE_OUT/audit_fixture" "$FX_BIN" >/dev/null 2>&1 || true
diff -u "$PTC_FIXTURES/expected_static.txt" "$FIXTURE_OUT/audit_fixture/missing_patch_targets.txt"

patch_status=0
for target in 0.107.1 0.111.0; do
  if [[ "$target" != "$CompatibilityTarget" ]]; then
    dotnet build "$PROJECT" -c Release -nologo -v q -clp:ErrorsOnly -p:CompatibilityTarget="$target"
  fi
  props="$(dotnet msbuild "$PROJECT" -nologo -p:CompatibilityTarget="$target" -p:Configuration=Release \
    -getProperty:TargetPath -getProperty:Sts2DataDir -getProperty:LibraryOfRuinaLibDll -getProperty:ActLikeIt2Dll -getProperty:RitsuLibRoot)"
  prop() { python3 -c 'import json,sys; print(json.loads(sys.argv[1])["Properties"][sys.argv[2]])' "$props" "$1"; }
  MOD_DLL="$(prop TargetPath)"
  RITSU_ROOT="$(prop RitsuLibRoot)"
  refs=("$(prop Sts2DataDir)" "$RITSU_ROOT/compat/$target" "$RITSU_ROOT/shared" "$(dirname "$(prop LibraryOfRuinaLibDll)")" "$(dirname "$(prop ActLikeIt2Dll)")")
  dotnet "$PTC" "$target" "$MOD_DLL" "$ROOT/tools/PatchTargetCheck/exceptions.txt" "$FIXTURE_OUT/patch_targets.$target.txt" "${refs[@]}" || patch_status=1
  dotnet "$AUDIT" "$MOD_DLL" "$FIXTURE_OUT/audit.$target" "${refs[@]}" >/dev/null || patch_status=1
  GUARD="$ROOT/src/infra/patching/vanilla_copy_guard.$target.txt"
  dotnet "$GUARD_TOOL" "$MOD_DLL" "$GUARD" "$FIXTURE_OUT/guard.$target.txt" "${refs[@]}" || patch_status=1
  diff -u --label "vanilla_copy_guard.$target.txt" --label "按 $target 实际补丁目标重算" \
    <(grep -v '^#' "$GUARD") <(grep -v '^#' "$FIXTURE_OUT/guard.$target.txt") || patch_status=1
done
if [[ "$patch_status" != 0 ]]; then
  echo "补丁目标检查失败：见上方 FAILED 行与守卫表差异" >&2
  exit 1
fi

# The skip-prefix scan must see every patch class form Harmony installs: the fixture program compares the
# shared PatchClassRules with Harmony itself, then the scan of the fixtures must match the expected list.
FIXTURES="$ROOT/tools/PatchRuleFixtures"
dotnet run --project "$FIXTURES/PatchRuleFixtures.csproj" -c Release
dotnet run --project "$ROOT/tools/ModSnapshot/ModSnapshot.csproj" -c Release -- \
  "$FIXTURES/bin/Release/net9.0/PatchRuleFixtures.dll" "$FIXTURE_OUT" "$FIXTURES/bin/Release/net9.0" >/dev/null
diff -u "$FIXTURES/expected_skip_prefixes.txt" "$FIXTURE_OUT/skip_prefixes.txt"
diff -u "$FIXTURES/expected_hook_patches.txt" "$FIXTURE_OUT/hook_patches.txt"

# Every bool prefix must say why it has to skip the original, and every Hook.* patch why it is not a model
# override (design philosophy §1/§3).
for list in skip_prefixes hook_patches; do
  if grep -q $'\tMISSING$' "$TMP/$list.txt"; then
    echo "$list without [LibraryPatch(Reason = ...)]:" >&2
    grep $'\tMISSING$' "$TMP/$list.txt" | cut -f1 >&2
    exit 1
  fi
done

# Types found by order-sensitive discovery (patches, card pools, relics, ally providers) must be listed in
# src/infra/helpers/type_discovery_order.txt; an unlisted one falls back to TypeDef order and moves whenever files move.
# Checked before --accept so accepting cannot hide it.
if grep -qE $'(\tunlisted$|^stale\t)' "$TMP/discovery_order.txt"; then
  echo "type_discovery_order.txt is out of date (add unlisted types in their intended position, drop stale keys):" >&2
  grep -E $'(\tunlisted$|^stale\t)' "$TMP/discovery_order.txt" >&2
  exit 1
fi

# 有状态的静态字段必须在 tools/static_state.txt 登记为局级、战斗级、按实例或无害；在 --accept 之前检查，接受快照也盖不住。
cat "$ROOT/tools/static_state.txt" "$ROOT/tools/static_state.$CompatibilityTarget.txt" > "$FIXTURE_OUT/static_state.registry"
python3 "$ROOT/tools/check_static_state.py" "$TMP/static_fields.txt" "$FIXTURE_OUT/static_state.registry"

if [[ "${1:-}" == "--accept" ]]; then
  mkdir -p "$BASELINE"
  cp "$TMP"/*.txt "$BASELINE/"
  echo "snapshots updated"
else
  diff -ru -x .gdignore -x .DS_Store -x headless "$BASELINE" "$TMP" && echo "snapshots unchanged ($CompatibilityTarget)"
fi
