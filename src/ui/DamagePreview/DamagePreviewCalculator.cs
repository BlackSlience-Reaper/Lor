using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryLib.Hooks;
using LibraryLib.Localization;
using LibraryLib.Localization.LibraryDynamicVars;
using LibraryLib.Models;
using LibraryLib.Utils.Resistance;
using LibraryOfRuina.content.reverberation.GearChurch;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using Godot;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using LibraryLib.Combat;

namespace LibraryOfRuina.ui.DamagePreview;

internal sealed record DamagePreviewResult(string Summary, string Details);

internal static class DamagePreviewCalculator
{
    internal static string Text(string key) => key switch
    {
        "Block" => DamagePreviewTrace.Icon("res://images/ui/combat/block.png"),
        "Dice" => "min:",
        "UpperBound" => "max:",
        "PreventValue" or "NoChaos" => "0:",
        "BaseValueAndResistanceOnly" => "◇",
        _ => ""
    };

    internal static DamagePreviewResult? Calculate(CardModel card, LibraryCreature target)
    {
        List<string> summaries = [];
        List<string> details = [];
        // 同一张卡的骰子按声明顺序依次命中：后续骰子沿用前序骰子结算后的目标状态（格挡、混乱值、混乱），
        // 最小值与最大值各自作为独立路径推进；非骰子的伤害变量仍按当前实际状态独立计算。
        PreviewTargetState initial = PreviewTargetState.From(target);
        PreviewTargetState minimumState = initial;
        PreviewTargetState maximumState = initial;
        foreach (DynamicVar variable in card.DynamicVars.Values)
        {
            if (!TryDescribe(variable, card, target, out decimal low, out decimal high,
                    out ValueProp props, out LibraryDamageType type, out Creature? dealer, out string baseDetails))
            {
                continue;
            }

            bool chained = variable is LibraryDice;
            // 多次使用的骰子等价于连续多次命中，每次命中单独成行并推进状态。
            int hits = variable is LibraryDice { EnableCustomUseTimes: true } multiUse ? multiUse.UseTimes : 1;
            for (int hit = 0; hit < hits; hit++)
            {
                PreviewTargetState minimumStart = chained ? minimumState : initial;
                PreviewTargetState maximumStart = chained ? maximumState : initial;
                DamageComponent minimum = CalculateComponent(card, target, dealer, low, props, type, minimumStart);
                bool separateMaximum = low != high || minimumStart != maximumStart;
                DamageComponent maximum = separateMaximum
                    ? CalculateComponent(card, target, dealer, high, props, type, maximumStart)
                    : minimum;
                string physicalMinimum = DescribeResistance(target, minimumStart, type, false);
                string physicalMaximum = DescribeResistance(target, maximumStart, type, false);
                string chaosMinimum = DescribeResistance(target, minimumStart, type, true);
                string chaosMaximum = DescribeResistance(target, maximumStart, type, true);
                string hp = Range(physicalMinimum, minimum.Hp, physicalMaximum, maximum.Hp, low, high, chained);
                string chaos = Range(chaosMinimum, minimum.Chaos, chaosMaximum, maximum.Chaos, low, high, chained);
                summaries.Add($"{hp}\n{chaos}");
                string minimumLabel = chained ? "min: " : "";
                details.Add($"{baseDetails}{minimumLabel}{physicalMinimum}{minimum.HpDetails}\n{minimumLabel}{chaosMinimum}{minimum.ChaosDetails}");
                if (separateMaximum || chained)
                {
                    details.Add($"max: {physicalMaximum}{maximum.HpDetails}\nmax: {chaosMaximum}{maximum.ChaosDetails}");
                }

                if (chained)
                {
                    minimumState = minimum.Next;
                    maximumState = maximum.Next;
                }
            }
        }

        if (summaries.Count == 0)
        {
            return null;
        }

        return new DamagePreviewResult(string.Join("\n", summaries), string.Join("\n", details));
    }

    private static bool TryDescribe(DynamicVar variable, CardModel card, Creature target,
        out decimal low, out decimal high, out ValueProp props, out LibraryDamageType type, out Creature? dealer,
        out string baseDetails)
    {
        low = high = variable.BaseValue;
        props = ValueProp.Move;
        // 与基础库实战用同一套推断（含其他模组注册的改判），预览与实际攻击分类一致。
        type = LibraryDamageTypes.ResolveForCard(card, target, isPreview: true);
        dealer = card.Owner.Creature;
        baseDetails = "";
        switch (variable)
        {
            case LibraryDice dice when dice.DiceType != LibraryDiceType.Block:
                var modifier = RolandDicePreviewModifiers.Get(card, dice);
                using (var trace = new DamagePreviewTrace(dice.BaseValue, Text("Dice")))
                {
                    if (modifier.Minimum != 0m)
                    {
                        trace.Add(modifier.Minimum, "");
                    }
                    low = LibraryHooks.ModifyDiceMinValue(card.CombatState!, dice, dice.BaseValue + modifier.Minimum);
                    low = decimal.Truncate(low);
                    trace.Set(low, Text("Rule"));
                    baseDetails = trace.Render() + "\n";
                }
                using (var trace = new DamagePreviewTrace(dice.BaseValue + dice.FloatValue, Text("UpperBound")))
                {
                    if (modifier.Maximum != 0m)
                    {
                        trace.Add(modifier.Maximum, "");
                    }
                    high = LibraryHooks.ModifyDiceMaxValue(card.CombatState!, dice, dice.BaseValue + dice.FloatValue + modifier.Maximum);
                    high = decimal.Truncate(high);
                    trace.Set(high, Text("Rule"));
                    baseDetails += trace.Render() + "\n";
                }
                low = Math.Min(low, high);
                type = dice.DamageType;
                break;
            case LibraryDamageVar damage:
                props = damage.Props;
                type = damage.DamageType;
                break;
            case LibraryCalculatedDamageVar calculated:
                low = high = calculated.Calculate(target);
                props = calculated.Props;
                type = calculated.DamageType;
                if (calculated.IsFromOsty)
                {
                    dealer = card.Owner.Osty;
                }
                break;
            case CalculatedDamageVar calculated:
                low = high = calculated.Calculate(target);
                props = calculated.Props;
                if (calculated.IsFromOsty)
                {
                    dealer = card.Owner.Osty;
                }
                break;
            case DamageVar damage:
                props = damage.Props;
                break;
            case LibraryOstyDamageVar osty:
                props = osty.Props;
                type = osty.DamageType;
                dealer = card.Owner.Osty;
                break;
            case OstyDamageVar osty:
                props = osty.Props;
                dealer = card.Owner.Osty;
                break;
            default:
                return false;
        }

        if (variable is CalculatedDamageVar
            && card.DynamicVars.TryGetValue("CalculationBase", out DynamicVar? basis)
            && card.DynamicVars.TryGetValue("ExtraDamage", out DynamicVar? extra))
        {
            string formula = extra.BaseValue == 0m
                ? DamagePreviewTrace.CompareNumber(low, low)
                : $"{DamagePreviewTrace.CompareNumber(basis.BaseValue, basis.BaseValue)} + {DamagePreviewTrace.CompareNumber(extra.BaseValue, 0m)} * {DamagePreviewTrace.CompareNumber((low - basis.BaseValue) / extra.BaseValue, (low - basis.BaseValue) / extra.BaseValue)} = {DamagePreviewTrace.CompareNumber(low, basis.BaseValue)}";
            baseDetails = $"◇ {formula}\n";
        }

        return dealer != null;
    }

    /// <summary>
    /// 按 <paramref name="state"/> 描述的目标状态计算单次命中，并返回命中后的状态供同卡后续骰子使用。
    /// Hook 仍读取实际生物；格挡、混乱值、混乱抗性覆盖等在本方法内按模拟状态结算，不改动实际战斗实体。
    /// </summary>
    private static DamageComponent CalculateComponent(CardModel card, LibraryCreature target, Creature? dealer,
        decimal amount, ValueProp props, LibraryDamageType type, PreviewTargetState state)
    {
        // 烟气增伤、清醒烟气和诺沃面料仍由生产 Hook 计算、由 trace 记录；这里只替换预览状态，避免重复乘算。
        using IDisposable hitPreview = GearChurchHitContext.Preview(target, dealer, state.SmokeStacks, state.FabricHits);
        var run = card.Owner.RunState;
        var combat = card.CombatState!;
        bool applyResistance = ResistancePreview.ShouldApplyResistance(props, type);
        decimal hp;
        string hpDetails;
        int blockAfterHit = state.Block;
        decimal hpBlocked;
        using (var trace = new DamagePreviewTrace(amount, ""))
        {
            // 有 Library 目标时，基础库把原版攻击转到 LibraryCreatureCmd，使用同一组 Hook。
            decimal modified = LibraryHooks.ModifyDamage(run, combat, target, dealer, amount, props, card,
                null, ModifyDamageHookType.All, CardPreviewMode.Normal, out _, type);
            trace.Set(modified, Text("Rule"));
            hpBlocked = props.HasFlag(ValueProp.Unblockable) ? 0m : Math.Min(state.Block, modified);
            if (hpBlocked > 0m)
            {
                trace.Add(-hpBlocked, Text("Block"));
            }

            // 实际结算 DamageBlockInternal 按整数扣除格挡。
            blockAfterHit = Math.Max(0, state.Block - (int)hpBlocked);
            decimal remaining = Math.Max(0m, modified - hpBlocked);
            if (applyResistance)
            {
                LibraryResistanceLevel resistance = PhysicalResistance(target, state, type);
                trace.Multiply(resistance.GetMultiplier(), DescribeResistance(target, state, type, false),
                    ResistanceMultiplier(resistance));
                remaining *= resistance.GetMultiplier();
            }

            hp = LibraryHooks.ModifyHpLostBeforeOsty(run, combat, target, remaining, props, dealer, card, out _, type);
            hp = LibraryHooks.ModifyHpLostAfterOsty(run, combat, target, hp, props, dealer, card, out _, type);
            trace.Set(hp, Text("Rule"));
            hp = Math.Max(0m, decimal.Truncate(hp));
            trace.Set(hp, Text("Round"));
            hpDetails = trace.Render();
        }

        // 腐蚀类能力：本次命中造成格挡或生命伤害后追加受到层数伤害，计入本次生命伤害并继续消耗格挡。
        int hitTotalDamage = (int)hpBlocked + (int)hp;
        foreach (ICorrosionFollowUpPower corrosion in target.Powers.OfType<ICorrosionFollowUpPower>())
        {
            if (!CorrosionFollowUpRules.TriggersOnHit(corrosion.Amount, hitTotalDamage, props))
            {
                continue;
            }

            hp += CalculateCorrosionFollowUp(corrosion, target, dealer, ref blockAfterHit, out string followUpDetails);
            hpDetails += "\n" + followUpDetails;
        }

        decimal chaos;
        string chaosDetails;
        int chaoValueAfterHit = state.ChaoValue;
        bool chaoedAfterHit = state.IsChaoed;
        using (var trace = new DamagePreviewTrace(amount, ""))
        {
            // AttackCommand 以本次基础伤害扣除命中前格挡，再进入混乱伤害 Hook。
            decimal blocked = props.HasFlag(ValueProp.Unblockable) && card is LibraryCardModel
                ? 0m
                : Math.Min(state.Block, Math.Max(0m, amount));
            if (blocked > 0m)
            {
                trace.Add(-blocked, Text("Block"));
            }

            chaos = Math.Max(0m, amount - blocked);
            if (!target.HasChaoResistance || state.IsChaoed || state.ChaoValue <= 0
                || (card is not LibraryCardModel
                    && (!props.HasFlag(ValueProp.Move) || props.HasFlag(ValueProp.Unpowered))))
            {
                chaos = 0m;
                trace.Set(chaos, Text("NoChaos"));
            }
            else
            {
                chaos = LibraryHooks.ModifyChaoDamage(run, combat, target, dealer, chaos, props, card,
                    null, ModifyChaoDamageHookType.All, CardPreviewMode.Normal, out _, type);
                trace.Set(chaos, Text("Rule"));
                if (applyResistance)
                {
                    LibraryResistanceLevel resistance = ChaosResistance(target, state, type);
                    trace.Multiply(resistance.GetMultiplier(), DescribeResistance(target, state, type, true),
                        ResistanceMultiplier(resistance));
                    chaos *= resistance.GetMultiplier();
                }

                chaos = Math.Max(0m, decimal.Truncate(chaos));
                trace.Set(chaos, Text("Round"));
                // 实际结算在混乱值归零时立即进入混乱：后续骰子面对致命抗性且不再造成混乱伤害。
                chaoValueAfterHit = Math.Max(0, state.ChaoValue - (int)chaos);
                chaoedAfterHit = chaoValueAfterHit == 0;
            }

            chaosDetails = trace.Render();
        }

        int smokeAfterHit = state.SmokeStacks;
        int fabricAfterHit = state.FabricHits;
        if (ValuePropCompat.IsPoweredAttack(props))
        {
            if (target.GetPower<GearChurchSoberSmokePower>() != null)
            {
                smokeAfterHit = Math.Max(0, smokeAfterHit - GearChurchRules.SoberSmokeCost);
            }
            if (target.GetPower<EileenNuovoFabricPower>() != null)
            {
                fabricAfterHit = Math.Min(GearChurchRules.FabricProtectedHits, fabricAfterHit + 1);
            }
        }
        return new DamageComponent(hp, chaos, hpDetails, chaosDetails,
            new PreviewTargetState(blockAfterHit, chaoValueAfterHit, chaoedAfterHit, smokeAfterHit, fabricAfterHit));
    }

    /// <summary>
    /// 腐蚀追加伤害为无强化伤害，实际结算走原版 CreatureCmd.Damage：不结算抗性与混乱伤害，只消耗格挡。
    /// </summary>
    private static decimal CalculateCorrosionFollowUp(ICorrosionFollowUpPower corrosion, LibraryCreature target,
        Creature? attacker, ref int block, out string details)
    {
        var run = target.CombatState!.RunState;
        var combat = target.CombatState;
        Creature? dealer = corrosion.GetHitFollowUpDealer(attacker);
        ValueProp props = CorrosionFollowUpRules.DamageProps;
        using IDisposable source = corrosion.EnterDamageSourceScope();
        using var trace = new DamagePreviewTrace(corrosion.Amount, DamagePreviewTrace.Name(corrosion));
        decimal modified = Math.Max(0m, Hook.ModifyDamage(run, combat, target, dealer, corrosion.Amount, props,
            null, null, ModifyDamageHookType.All, CardPreviewMode.None, out _));
        trace.Set(modified, Text("Rule"));
        decimal blocked = Math.Min(block, modified);
        if (blocked > 0m)
        {
            trace.Add(-blocked, Text("Block"));
        }

        block = Math.Max(0, block - (int)blocked);
        decimal hp = Hook.ModifyHpLost(run, combat, target, Math.Max(0m, modified - blocked), props, dealer, null,
            HpLossHookPhase.All, out _);
        trace.Set(hp, Text("Rule"));
        hp = Math.Max(0m, decimal.Truncate(hp));
        trace.Set(hp, Text("Round"));
        details = trace.Render();
        return hp;
    }

    // 混乱后的实际结算把全部抗性覆盖为致命，预览沿用同一规则；未混乱时读取实际抗性。
    private static LibraryResistanceLevel PhysicalResistance(LibraryCreature target, PreviewTargetState state,
        LibraryDamageType type) =>
        state.IsChaoed ? LibraryResistanceLevel.Fatal : target.GetPhysicalResistanceLevel(type);

    private static LibraryResistanceLevel ChaosResistance(LibraryCreature target, PreviewTargetState state,
        LibraryDamageType type) =>
        state.IsChaoed ? LibraryResistanceLevel.Fatal : target.GetChaosResistanceLevel(type);

    private static string DescribeResistance(LibraryCreature target, PreviewTargetState state, LibraryDamageType type,
        bool chaos)
    {
        if (type == LibraryDamageType.None || (chaos && !target.HasChaoResistance))
        {
            return chaos ? "[gold]◇[/gold]" : "[red]◇[/red]";
        }

        LibraryResistanceLevel level = chaos ? ChaosResistance(target, state, type) : PhysicalResistance(target, state, type);
        return ResistanceIcon(type, level, chaos);
    }

    internal static string ResistanceIcon(LibraryDamageType type, LibraryResistanceLevel level, bool chaos)
    {
        string chaosPart = chaos ? "_chaos" : "";
        return DamagePreviewTrace.Icon($"res://LibraryOfRuinaLib/images/resistance/{type.String()}{chaosPart}_{level.GetLocKeySuffix()}.png");
    }

    internal static string ResistanceMultiplier(LibraryResistanceLevel level)
    {
        string value = $"{DamagePreviewTrace.Number(level.GetMultiplier() * 100m)}%";
        // 沿用当前语言抗性类别文案的颜色标签，避免维护另一套颜色映射。
        string text = new LocString("powers", $"DAMAGE_TYPE_RESISTANCE.{level.GetLocKeySuffix()}").GetRawText();
        int end = text.IndexOf(']');
        if (text.StartsWith("[", StringComparison.Ordinal) && end > 1)
        {
            string tag = text.Substring(1, end - 1);
            if (tag.StartsWith("color=", StringComparison.Ordinal))
            {
                return $"[{tag}]{value}[/color]";
            }

            if (tag is "gray" or "grey")
            {
                return DamagePreviewTrace.Tint(value, MegaCrit.Sts2.Core.Helpers.StsColors.gray);
            }

            if (tag is "red" or "green" or "blue" or "orange" or "gold")
            {
                return $"[{tag}]{value}[/{tag}]";
            }
        }

        return DamagePreviewTrace.Tint(value, Colors.White);
    }

    private static string Range(string minimumIcon, decimal low, string maximumIcon, decimal high,
        decimal baseLow, decimal baseHigh, bool isDice)
    {
        string minimum = $"{minimumIcon}{DamagePreviewTrace.CompareNumber(low, baseLow)}";
        bool sameIcon = minimumIcon == maximumIcon;
        if (!isDice && low == high && baseLow == baseHigh && sameIcon)
        {
            return minimum;
        }

        // 最大值路径的抗性与最小值路径不同（例如前序骰子最大值已使目标混乱）时，在最大值前补充其图标。
        string maximum = sameIcon ? "" : maximumIcon;
        return $"{minimum}~{maximum}{DamagePreviewTrace.CompareNumber(high, baseHigh)}";
    }

    /// <summary>同一张卡前序骰子结算后的目标状态快照，仅供预览推进，不改动实际生物。</summary>
    private readonly record struct PreviewTargetState(int Block, int ChaoValue, bool IsChaoed,
        int SmokeStacks, int FabricHits)
    {
        internal static PreviewTargetState From(LibraryCreature target) =>
            new(target.Block, target.CurrentChaoValue, target.IsChaoed,
                target.GetPower<GearChurchSmokePower>()?.Amount ?? 0,
                target.GetPower<EileenNuovoFabricPower>()?.HitsReceived ?? 0);
    }

    private sealed record DamageComponent(decimal Hp, decimal Chaos, string HpDetails, string ChaosDetails,
        PreviewTargetState Next);
}
