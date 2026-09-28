using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using LibraryLib.Combat;
using LibraryLib.Hooks;
using LibraryLib.Powers;
using LibraryLib.Utils.Resistance;
using LibraryOfRuina.patches;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.ValueProps;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.ui.DamagePreview;

/// <summary>只观察同步预览期间的 Hook 返回值，保留游戏原有调用顺序与返回值。</summary>
internal sealed class DamagePreviewTrace : IDisposable
{
    [ThreadStatic]
    private static DamagePreviewTrace? _current;

    private readonly DamagePreviewTrace? _previous;
    private readonly decimal _baseValue;

    internal string Formula { get; private set; }

    internal decimal Value { get; private set; }

    internal DamagePreviewTrace(decimal value, string source)
    {
        _previous = _current;
        _current = this;
        _baseValue = value;
        Value = value;
        Formula = $"{source}{CompareNumber(value, value)}";
    }

    public void Dispose()
    {
        _current = _previous;
    }

    internal static string Number(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    // 基础值与相等结果为白色，增伤为原版绿色，减伤为原版红色。
    internal static string CompareNumber(decimal value, decimal basis) =>
        Tint(Number(value), ComparisonColor(value, basis));

    private static Color ComparisonColor(decimal value, decimal basis)
    {
        if (value > basis)
        {
            return StsColors.green;
        }

        if (value < basis)
        {
            return StsColors.red;
        }

        return Colors.White;
    }

    internal static string Tint(string text, Color color) =>
        $"[color=#{color.ToHtml(false)}]{text}[/color]";

    internal static string Icon(string path) =>
        ResourceLoader.Exists(path) ? $"[img width=24 height=24]{path}[/img]" : "◇";

    internal static string Name(object source) => source switch
    {
        PowerModel power when PowerIconResolver.TryResolve(power, out ResolvedPowerIcon icon) => Icon(icon.Path),
        RelicModel relic => Icon(relic.PackedIconPath),
        // 充能球沿用游戏内充能球栏显示的图标资源。
        OrbModel orb => VanillaPrivate.OrbModelIconPath.Get(orb) is { } orbIcon ? Icon(orbIcon) : "◇",
        CardModel => "◇",
        MonsterModel => "◆",
        EnchantmentModel enchantment => Icon(enchantment.IconPath),
        _ => "◇"
    };

    // 供 IL 插入的观察调用使用；实际战斗没有当前 trace，立即返回。
    internal static void Record(object source, decimal input, decimal result, string method)
    {
        DamagePreviewTrace? trace = _current;
        if (trace == null)
        {
            return;
        }

        if (method.EndsWith("Additive", StringComparison.Ordinal))
        {
            if (result != 0m)
            {
                trace.Add(result, Name(source));
            }
        }
        else if (method.EndsWith("Multiplicative", StringComparison.Ordinal))
        {
            if (result != 1m)
            {
                trace.Multiply(result, Name(source));
            }
        }
        else if (method.EndsWith("Cap", StringComparison.Ordinal))
        {
            if (result < trace.Value)
            {
                trace.Set(result, Name(source));
            }
        }
        else if (input != result)
        {
            if (source is LibraryProtectionPower or LibraryVulnerablePower)
            {
                decimal adjustment = result - input;
                trace.Add(adjustment, Name(source));
                trace.Value = result;
            }
            else
            {
                trace.Set(result, Name(source));
            }
        }
    }

    internal static void RecordPolicy(object source, LibraryCombatValueResolution resolution)
    {
        if (_current != null && resolution != LibraryCombatValueResolution.Default)
        {
            _current.Formula += $" {Name(source)}{DamagePreviewCalculator.Text(resolution.ToString())}";
        }
    }

    /// <summary>
    /// 替换各数值 Hook 内对 <see cref="LibraryCombatValueResolver.Resolve"/> 的调用，参数与返回值完全一致。
    /// Resolve 内部以 in 参数调用策略接口，Android 的 Mono 运行时无法为这类调用生成 Harmony 替换方法，
    /// 因此不再修补 Resolve 本身：实际战斗没有当前 trace 时原样委托；预览时按同一顺序遍历策略并记录来源。
    /// </summary>
    internal static LibraryCombatValueResolution ResolveWithPolicyTrace(
        ICombatState? combatState,
        LibraryCombatValueKind kind,
        decimal baseValue,
        Creature? target,
        Creature? dealer,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType damageType,
        CardPreviewMode previewMode)
    {
        if (_current == null || combatState == null)
        {
            return LibraryCombatValueResolver.Resolve(
                combatState, kind, baseValue, target, dealer, props, cardSource, cardPlay, damageType, previewMode);
        }

        var context = new LibraryCombatValueContext(
            combatState, kind, baseValue, target, dealer, props, cardSource, cardPlay, damageType, previewMode);
        LibraryCombatValueResolution result = LibraryCombatValueResolution.Default;
        foreach (AbstractModel listener in combatState.IterateHookListeners())
        {
            if (listener is not ILibraryCombatValueResolutionPolicy policy)
            {
                continue;
            }

            LibraryCombatValueResolution resolution = policy.GetCombatValueResolution(in context);
            RecordPolicy(policy, resolution);
            result = LibraryCombatValueResolver.MostRestrictive(result, resolution);
            if (result == LibraryCombatValueResolution.PreventValue)
            {
                break;
            }
        }

        return result;
    }

    internal void Add(decimal amount, string source)
    {
        Formula = $"({Formula}{(amount < 0m ? "-" : "+")}{source}{Tint(Number(Math.Abs(amount)), ComparisonColor(amount, 0m))})";
        Value += amount;
    }

    internal void Multiply(decimal multiplier, string source, string? multiplierText = null)
    {
        string factor = multiplierText
            ?? Tint($"{Number(multiplier * 100m)}%", ComparisonColor(multiplier, 1m));
        Formula = $"{Formula} * {source}{factor}";
        Value *= multiplier;
    }

    internal void Set(decimal value, string source)
    {
        if (Value == value)
        {
            return;
        }

        Formula = $"({Formula}) -> {source}{CompareNumber(value, _baseValue)}";
        Value = value;
    }

    internal string Render() => $"{Formula} = {CompareNumber(Value, _baseValue)}";

    /// <summary>以调用方格式化好的结果结尾，例如受伤预览把失去生命值显示为负数。</summary>
    internal string RenderResult(string result) => $"{Formula} = {result}";
}

/// <summary>
/// 在固定的数值 Hook 入口观察每次模型调用，无需扫描模型或二次执行能力。
/// 栈上的参数保存后原样恢复，返回值也原样传回原始计算。
/// 不修补 <see cref="LibraryCombatValueResolver.Resolve"/> 本身：它的方法体经 in 参数调用策略接口，
/// Android 的 Mono 运行时无法为其生成 Harmony 替换方法；Hook 内对它的调用改为同签名的
/// <see cref="DamagePreviewTrace.ResolveWithPolicyTrace"/>，由后者记录策略来源。
/// </summary>
[HarmonyPatch]
internal static class DamagePreviewTracePatch
{
    private static readonly MethodInfo ResolveWithPolicyTraceMethod =
        AccessTools.Method(typeof(DamagePreviewTrace), nameof(DamagePreviewTrace.ResolveWithPolicyTrace));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(FinalHpLossClamp), nameof(FinalHpLossClamp.Apply));
        foreach (string name in new[]
                 {
                     "ModifyDamage", "ModifyDamageInternal", "ModifyChaoDamage", "ModifyChaoDamageInternal",
                     "ModifyHpLostBeforeOsty", "ModifyHpLostAfterOsty", "ModifyDiceMinValue", "ModifyDiceMaxValue"
                 })
        {
            yield return AccessTools.Method(typeof(LibraryHooks), name);
        }
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        MethodInfo record = AccessTools.Method(typeof(DamagePreviewTrace), nameof(DamagePreviewTrace.Record));
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call
                && instruction.operand is MethodInfo
                {
                    DeclaringType: { } declaringType,
                    Name: nameof(LibraryCombatValueResolver.Resolve)
                }
                && declaringType == typeof(LibraryCombatValueResolver))
            {
                // 同签名静态方法直接替换操作数，标签与异常块保持不变。
                instruction.operand = ResolveWithPolicyTraceMethod;
                yield return instruction;
                continue;
            }

            if (instruction.opcode != OpCodes.Callvirt
                || instruction.operand is not MethodInfo method
                || method.ReturnType != typeof(decimal)
                || !(method.Name.StartsWith("ModifyDamage", StringComparison.Ordinal)
                    || method.Name.StartsWith("ModifyChaoDamage", StringComparison.Ordinal)
                    || method.Name.StartsWith("ModifyHpLost", StringComparison.Ordinal)
                    || method.Name.StartsWith("Enchant", StringComparison.Ordinal)
                    || method.Name == "ClampFinalHpLoss"
                    || method.Name.StartsWith("ModifyDice", StringComparison.Ordinal)))
            {
                yield return instruction;
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            LocalBuilder[] arguments = parameters.Select(p => generator.DeclareLocal(p.ParameterType)).ToArray();
            LocalBuilder receiver = generator.DeclareLocal(method.DeclaringType!);
            LocalBuilder result = generator.DeclareLocal(typeof(decimal));
            var prefix = new List<CodeInstruction>();
            for (int i = arguments.Length - 1; i >= 0; i--)
            {
                prefix.Add(new CodeInstruction(OpCodes.Stloc, arguments[i]));
            }

            prefix.Add(new CodeInstruction(OpCodes.Stloc, receiver));
            prefix[0].MoveLabelsFrom(instruction);
            prefix[0].MoveBlocksFrom(instruction);
            foreach (CodeInstruction save in prefix)
            {
                yield return save;
            }

            yield return new CodeInstruction(OpCodes.Ldloc, receiver);
            foreach (LocalBuilder argument in arguments)
            {
                yield return new CodeInstruction(OpCodes.Ldloc, argument);
            }

            yield return instruction;
            yield return new CodeInstruction(OpCodes.Stloc, result);
            yield return new CodeInstruction(OpCodes.Ldloc, receiver);
            int amountIndex = Array.FindIndex(parameters, p => p.ParameterType == typeof(decimal));
            yield return new CodeInstruction(OpCodes.Ldloc, amountIndex >= 0 ? arguments[amountIndex] : result);
            yield return new CodeInstruction(OpCodes.Ldloc, result);
            yield return new CodeInstruction(OpCodes.Ldstr, method.Name);
            yield return new CodeInstruction(OpCodes.Call, record);
            yield return new CodeInstruction(OpCodes.Ldloc, result);
        }
    }
}

/// <summary>
/// 以同一转译观察原版伤害与失去生命值 Hook，供我方受伤预览的详细视图列出各能力与遗物的修正。
/// 与基础库 Hook 的补丁分开应用，单独失败时不影响敌人伤害预览。
/// 不能改成预览里自己遍历监听者：那要把原版的三轮/四轮修正再实现一遍并二次调用各模型，还看不到其他模组对 Hook 的补丁。
/// </summary>
[HarmonyPatch]
[LibraryPatch(Optional = true, Reason = "原版修正钩子不暴露逐个监听者的中间值；只在同步预览、有当前轨迹时记录，不改变参数和返回值。失败只丢预览详情里的逐项来源。")]
internal static class VanillaHookPreviewTracePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Hooks.Hook), "ModifyDamageInternal");
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.ModifyHpLost));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) =>
        DamagePreviewTracePatch.Transpiler(instructions, generator);
}
