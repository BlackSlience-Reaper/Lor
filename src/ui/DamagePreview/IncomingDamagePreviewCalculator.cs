using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibraryLib.Combat.HealthBars;
using LibraryLib.Entities.Creatures;
using LibraryLib.Hooks;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.ui.DamagePreview;

/// <summary>一名受击者在下一次敌方回合结束前的受伤预览。</summary>
internal sealed record IncomingDamagePreviewResult(
    int Blocked,
    int HpLoss,
    bool IsLethal,
    string Summary,
    string Details);

/// <summary>
/// 按实际结算顺序模拟本地玩家回合结束与随后的敌方回合：回合结束格挡、自伤与持续伤害，
/// 再按敌人行动顺序逐次命中。伤害、格挡与失去生命值全部经由原版或基础库 Hook，
/// 因此所有能力、遗物、附魔与敌方修正都按其当前状态参与计算；
/// 受击者状态只在模拟对象内推进，不修改实际战斗数据。
/// </summary>
internal static class IncomingDamagePreviewCalculator
{
    private const string HeartIconPath = "res://images/atlases/ui_atlas.sprites/top_bar/top_bar_heart.tres";
    // 致命标记沿用原版致命一击意图图标，资源不可用时改用红骷髅遗物图标。
    private const string LethalIconPath = "res://images/atlases/intent_atlas.sprites/intent_death_blow.tres";
    private const string LethalFallbackIconPath = "res://images/atlases/relic_atlas.sprites/red_skull.tres";
    private const string StaggerIconPath = "res://LibraryOfRuinaLib/images/powers/library_stagger_resistance.png";

    internal static string BlockIcon => DamagePreviewCalculator.Text("Block");

    internal static string HeartIcon => DamagePreviewTrace.Icon(HeartIconPath);

    internal static string StaggerIcon => DamagePreviewTrace.Icon(StaggerIconPath);

    private static string LethalIcon => DamagePreviewTrace.Icon(
        ResourceLoader.Exists(LethalIconPath) ? LethalIconPath : LethalFallbackIconPath);

    /// <summary>失去生命值统一显示为负数：大于 0 为红色，0 为常规文字色。</summary>
    internal static string HpLossText(int hpLoss) =>
        DamagePreviewTrace.Tint($"-{hpLoss}", hpLoss > 0 ? StsColors.red : StsColors.cream);

    /// <summary>
    /// 计算 <paramref name="targets"/> 中每名受击者的预览；本地玩家额外结算自己回合结束的效果。
    /// 没有任何伤害来源的受击者不出现在结果中。
    /// </summary>
    internal static IReadOnlyDictionary<Creature, IncomingDamagePreviewResult> Calculate(
        CombatState combat,
        Creature? localPlayer,
        IReadOnlyList<Creature> targets)
    {
        var simulation = new IncomingDamageSimulation(combat, targets);
        using (IncomingDamageSimulation.Enter(simulation))
        {
            if (localPlayer != null && simulation.TryGetState(localPlayer, out IncomingDamageTargetState? playerState))
            {
                SimulatePlayerTurnEnd(simulation, playerState!);
            }

            simulation.BeginEnemyTurn();
            SimulateEnemyAttacks(simulation);
        }

        var results = new Dictionary<Creature, IncomingDamagePreviewResult>();
        foreach (IncomingDamageTargetState state in simulation.States)
        {
            if (state.HasIncomingDamage)
            {
                results[state.Creature] = BuildResult(state);
            }
        }

        return results;
    }

    private static IncomingDamagePreviewResult BuildResult(IncomingDamageTargetState state)
    {
        bool isLethal = state.Hp <= 0;
        string blocked = DamagePreviewTrace.Tint(state.Blocked.ToString(), StsColors.blue);
        string hpLoss = HpLossText(state.HpLoss);
        string lethal = isLethal ? LethalIcon : "";
        string summary = $"{BlockIcon}{blocked}\n{HeartIcon}{hpLoss}{lethal}";
        string total = $"= {BlockIcon}{blocked} {HeartIcon}{hpLoss}{lethal}";
        string details = string.Join("\n", state.Details.Append(total));
        return new IncomingDamagePreviewResult(state.Blocked, state.HpLoss, isLethal, summary, details);
    }

    /// <summary>
    /// 玩家回合结束：先按原版 BeforeSideTurnEnd 顺序获得格挡（奥利哈钢在其余格挡之前检查是否没有格挡），
    /// 再结算充能球被动与手牌回合结束效果，最后是 AfterSideTurnEnd 的持续伤害。
    /// 这些条目排在敌人攻击标题之前，只用来源图标区分，不附加文字。
    /// </summary>
    private static void SimulatePlayerTurnEnd(IncomingDamageSimulation simulation, IncomingDamageTargetState state)
    {
        Creature creature = state.Creature;
        Player? player = creature.Player;
        if (player?.PlayerCombatState == null)
        {
            return;
        }

        bool hadNoBlockBeforeTurnEnd = state.Block <= 0;
        foreach (PlatingPower plating in creature.Powers.OfType<PlatingPower>())
        {
            simulation.GainBlock(state, plating.Amount, ValueProp.Unpowered, DamagePreviewTrace.Name(plating));
        }

        foreach (RelicModel relic in player.Relics)
        {
            switch (relic)
            {
                case CloakClasp clasp:
                    int handCount = player.PlayerCombatState!.Hand.Cards.Count;
                    if (handCount > 0)
                    {
                        simulation.GainBlock(
                            state,
                            (int)(handCount * clasp.DynamicVars.Block.BaseValue),
                            ValueProp.Unpowered,
                            DamagePreviewTrace.Name(clasp));
                    }
                    break;
                case Orichalcum or FakeOrichalcum when hadNoBlockBeforeTurnEnd:
                    simulation.GainBlock(state, relic.DynamicVars.Block, DamagePreviewTrace.Name(relic));
                    break;
                // 本回合打出攻击牌后涟漪水盆转为普通状态，与其回合结束判定一致。
                case RippleBasin { Status: RelicStatus.Active } basin:
                    simulation.GainBlock(state, basin.DynamicVars.Block, DamagePreviewTrace.Name(basin));
                    break;
            }
        }

        foreach (Player owner in simulation.Combat.Players)
        {
            // 冬眠使该玩家的冰霜球被动同时为其他玩家提供格挡。
            if (owner != player && !GameApi.HasHibernate(owner.Creature))
            {
                continue;
            }

            foreach (FrostOrb frost in owner.PlayerCombatState?.OrbQueue.Orbs.OfType<FrostOrb>() ?? [])
            {
                string icon = DamagePreviewTrace.Name(frost);
                int triggers = Hook.ModifyOrbPassiveTriggerCount(simulation.Combat, frost, 1, out _);
                for (int trigger = 0; trigger < triggers; trigger++)
                {
                    simulation.GainBlock(state, frost.PassiveVal, ValueProp.Unpowered, icon);
                }
            }
        }

        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToArray())
        {
            if (!card.HasTurnEndInHandEffect || !TryGetTurnEndSelfDamage(card, out decimal amount, out ValueProp props))
            {
                continue;
            }

            simulation.ResolveHit(
                state, amount, props, creature, card, LibraryDamageType.None, "", applyDamageHooks: true);
        }

        foreach (PowerModel power in creature.Powers.ToArray())
        {
            if (state.IsDown)
            {
                return;
            }

            switch (power)
            {
                case ICorrosionFollowUpPower corrosion when corrosion.Amount > 0:
                    using (corrosion.EnterDamageSourceScope())
                    {
                        simulation.ResolveHit(
                            state,
                            corrosion.Amount,
                            CorrosionFollowUpRules.DamageProps,
                            corrosion.GetTurnEndDamageDealer(),
                            null,
                            LibraryDamageType.None,
                            DamagePreviewTrace.Name(power),
                            applyDamageHooks: true);
                    }

                    state.CorrosionStacks[corrosion] = CorrosionFollowUpRules.StacksAfterTurnEnd(corrosion.Amount);
                    break;
                // 烧伤在所属方回合结束时按有效层数造成伤害；数值与生命条预测共用同一来源。
                case LibraryBurnPower burn:
                    foreach (LibraryHealthBarDamageForecast forecast in burn.GetLibraryHealthBarDamageForecasts(
                                 new LibraryHealthBarForecastContext(creature)))
                    {
                        simulation.ResolveHit(
                            state,
                            forecast.Damage,
                            forecast.Props,
                            forecast.Dealer,
                            forecast.CardSource,
                            LibraryDamageType.None,
                            DamagePreviewTrace.Name(power),
                            applyDamageHooks: true);
                    }
                    break;
            }
        }
    }

    /// <summary>手牌回合结束自伤沿用原版约定：伤害变量为 Damage，失去生命值变量为不可格挡的 HpLoss。</summary>
    private static bool TryGetTurnEndSelfDamage(CardModel card, out decimal amount, out ValueProp props)
    {
        if (card.DynamicVars.TryGetValue("Damage", out DynamicVar? damage) && damage is DamageVar damageVar)
        {
            amount = damageVar.BaseValue;
            props = damageVar.Props;
            return amount > 0m;
        }

        if (card.DynamicVars.TryGetValue("HpLoss", out DynamicVar? hpLoss) && hpLoss is HpLossVar)
        {
            amount = hpLoss.BaseValue;
            props = ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move;
            return amount > 0m;
        }

        amount = 0m;
        props = ValueProp.Unpowered;
        return false;
    }

    /// <summary>按敌人行动顺序结算每个攻击意图的每次命中；反击意图在玩家回合内触发，不计入敌方回合。</summary>
    private static void SimulateEnemyAttacks(IncomingDamageSimulation simulation)
    {
        CombatState combat = simulation.Combat;
        IReadOnlyList<Creature> ordinaryTargets = combat.Players.Select(static player => player.Creature).ToArray();
        foreach (Creature enemy in combat.Enemies.ToArray())
        {
            if (!enemy.IsAlive || enemy.Monster == null || AllyTurnRegistry.IsAllyCreature(enemy))
            {
                continue;
            }

            foreach (AbstractIntent intent in enemy.Monster.NextMove.Intents)
            {
                if (intent is not AttackIntent attack || intent is ICounterIntent)
                {
                    continue;
                }

                IReadOnlyList<Creature> targets = TargetedIntentIndicatorPatch.ResolveTargets(intent, enemy, ordinaryTargets);
                if (!targets.Any(simulation.IsTracked))
                {
                    continue;
                }

                SimulateAttack(simulation, enemy, attack, ordinaryTargets, targets);
            }
        }
    }

    private static void SimulateAttack(
        IncomingDamageSimulation simulation,
        Creature enemy,
        AttackIntent attack,
        IReadOnlyList<Creature> ordinaryTargets,
        IReadOnlyList<Creature> targets)
    {
        int hits = Math.Max(1, attack.Repeats);
        // 有基础伤害时由各受击者自己的 Hook 修正；占位为 0 的预览意图改用其显示值（已含修正）。
        decimal baseDamage = attack.DamageCalc?.Invoke() ?? 0m;
        bool applyDamageHooks = baseDamage > 0m;
        decimal amount = applyDamageHooks
            ? baseDamage
            : (decimal)attack.GetTotalDamage(ordinaryTargets, enemy) / hits;
        if (amount <= 0m)
        {
            return;
        }

        // 原版攻击命中 Library 生物时，单体按打击、多目标按斩击结算抗性（与基础库执行时判定一致）。
        LibraryDamageType type = targets.Count > 1 ? LibraryDamageType.Slash : LibraryDamageType.Blunt;
        string header = DamagePreviewTrace.Tint(enemy.Name, StsColors.gold);
        var headerAdded = new HashSet<IncomingDamageTargetState>();
        for (int hit = 0; hit < hits; hit++)
        {
            foreach (Creature target in targets)
            {
                Creature receiver = LibraryHooks.ModifyDamageTarget(
                    simulation.Combat, target, amount, ValueProp.Move, enemy, type);
                if (!simulation.TryGetState(receiver, out IncomingDamageTargetState? state) || state!.IsDown)
                {
                    continue;
                }

                if (headerAdded.Add(state))
                {
                    state.Details.Add(header);
                }

                IncomingHitOutcome outcome = simulation.ResolveHit(
                    state, amount, ValueProp.Move, enemy, null, type, "", applyDamageHooks);
                simulation.ResolveCorrosionFollowUps(state, outcome, ValueProp.Move, enemy);
            }
        }
    }
}

internal readonly record struct IncomingHitOutcome(int Blocked, int HpLoss, int TotalDamage);

/// <summary>模拟期间单个受击者的格挡、生命、混乱与次数进度。</summary>
internal sealed class IncomingDamageTargetState
{
    internal IncomingDamageTargetState(Creature creature)
    {
        Creature = creature;
        Block = Math.Max(0, (creature.PetOwner?.Creature ?? creature).Block);
        Hp = creature.CurrentHp;
        if (creature is LibraryCreature { IsPlayer: false } library)
        {
            ChaoValue = library.CurrentChaoValue;
            IsChaoed = library.IsChaoed;
        }
    }

    internal Creature Creature { get; }

    internal int Block { get; set; }

    internal int Hp { get; set; }

    internal int ChaoValue { get; set; }

    internal bool IsChaoed { get; set; }

    internal int Blocked { get; set; }

    internal int HpLoss { get; set; }

    // 坚硬外壳：当前一方回合内已失去的生命值。
    internal int HpLostThisSide { get; set; }

    // 跳动残骸：自本名玩家回合开始后失去的生命值。
    internal int HpLostSinceOwnerTurnStart { get; set; }

    internal bool HasIncomingDamage { get; set; }

    internal bool IsDown => Hp <= 0;

    internal Dictionary<ICorrosionFollowUpPower, int> CorrosionStacks { get; } = [];

    internal List<string> Details { get; } = [];
}

/// <summary>
/// 一次预览的模拟上下文。<see cref="Current"/> 只在同步计算期间存在，
/// 供有次数或每回合上限的受伤修正读取模拟进度（见 <see cref="IncomingDamagePreviewModelPatches"/>）。
/// </summary>
internal sealed class IncomingDamageSimulation
{
    [ThreadStatic]
    private static IncomingDamageSimulation? _current;

    private readonly Dictionary<Creature, IncomingDamageTargetState> _states = [];
    private readonly Dictionary<AbstractModel, int> _triggerCounts = [];
    private readonly Dictionary<Creature, int> _redirectHp = [];
    private readonly HashSet<BlackSwanDreamPageRelic> _dearFamilySlipperyCandidates = [];
    private readonly HashSet<BlackSwanDreamPageRelic> _dearFamilySlipperyConsumptions = [];

    internal IncomingDamageSimulation(CombatState combat, IReadOnlyList<Creature> targets)
    {
        Combat = combat;
        Run = combat.RunState;
        foreach (Creature target in targets)
        {
            _states[target] = new IncomingDamageTargetState(target);
        }
    }

    internal static IncomingDamageSimulation? Current => _current;

    internal CombatState Combat { get; }

    internal IRunState Run { get; }

    internal bool IsEnemyTurn { get; private set; }

    internal IEnumerable<IncomingDamageTargetState> States => _states.Values;

    internal static IDisposable Enter(IncomingDamageSimulation simulation)
    {
        IncomingDamageSimulation? previous = _current;
        _current = simulation;
        return new Scope(previous);
    }

    internal bool IsTracked(Creature creature) => _states.ContainsKey(creature);

    internal bool TryGetState(Creature creature, out IncomingDamageTargetState? state) =>
        _states.TryGetValue(creature, out state);

    /// <summary>敌方回合开始：坚硬外壳等按一方回合计数的上限重新开始。</summary>
    internal void BeginEnemyTurn()
    {
        IsEnemyTurn = true;
        foreach (IncomingDamageTargetState state in _states.Values)
        {
            state.HpLostThisSide = 0;
        }
    }

    internal int GetTriggerCount(AbstractModel model) => _triggerCounts.GetValueOrDefault(model);

    internal void GainBlock(IncomingDamageTargetState state, BlockVar block, string source)
    {
        GainBlock(state, block.BaseValue, block.Props, source);
    }

    /// <summary>与 CreatureCmd.GainBlock 相同：经格挡 Hook 修正后按整数累加。</summary>
    internal void GainBlock(IncomingDamageTargetState state, decimal amount, ValueProp props, string source)
    {
        decimal modified = Math.Max(0m, Hook.ModifyBlock(Combat, state.Creature, amount, props, null, null, out _));
        int before = state.Block;
        state.Block = (int)Math.Min(state.Block + modified, 999999999m);
        int gained = state.Block - before;
        if (gained > 0)
        {
            state.Details.Add($"{source}{IncomingDamagePreviewCalculator.BlockIcon}+{DamagePreviewTrace.CompareNumber(gained, amount)}");
        }
    }

    /// <summary>
    /// 按 CreatureCmd.Damage / LibraryCreatureCmd.Damage 的顺序结算一次命中：伤害修正、格挡、抗性、
    /// 失去生命值修正与伤害转移，并推进格挡、生命、混乱与次数进度。
    /// </summary>
    internal IncomingHitOutcome ResolveHit(
        IncomingDamageTargetState state,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type,
        string source,
        bool applyDamageHooks)
    {
        state.HasIncomingDamage = true;
        Creature target = state.Creature;
        // 强化攻击命中非玩家的 Library 生物时由基础库结算抗性与混乱；其余伤害走原版管线。
        bool libraryPipeline = target is LibraryCreature { IsPlayer: false }
            && ValuePropCompat.IsPoweredAttack(props);
        LibraryDamageType pipelineType = libraryPipeline ? type : LibraryDamageType.None;
        int blockBeforeHit = state.Block;
        decimal blocked;
        int hpLoss;
        using (var trace = new DamagePreviewTrace(amount, source))
        {
            decimal damage = amount;
            if (applyDamageHooks)
            {
                damage = libraryPipeline
                    ? LibraryHooks.ModifyDamage(Run, Combat, target, dealer, amount, props, cardSource, null,
                        ModifyDamageHookType.All, CardPreviewMode.None, out _, pipelineType)
                    : GameApi.ModifyDamage(Run, Combat, target, dealer, amount, props, cardSource, null,
                        ModifyDamageHookType.All, CardPreviewMode.None, out _);
                trace.Set(damage, "");
            }

            damage = Math.Max(0m, damage);
            blocked = props.HasFlag(ValueProp.Unblockable) ? 0m : Math.Min(state.Block, damage);
            if (blocked > 0m)
            {
                trace.Add(-blocked, IncomingDamagePreviewCalculator.BlockIcon);
            }

            state.Block = Math.Max(0, state.Block - (int)blocked);
            decimal unblocked = Math.Max(damage - blocked, 0m);
            if (libraryPipeline && ResistancePreview.ShouldApplyResistance(props, pipelineType))
            {
                LibraryResistanceLevel resistance = state.IsChaoed
                    ? LibraryResistanceLevel.Fatal
                    : ((LibraryCreature)target).GetPhysicalResistanceLevel(pipelineType);
                trace.Multiply(
                    resistance.GetMultiplier(),
                    DamagePreviewCalculator.ResistanceIcon(pipelineType, resistance, false),
                    DamagePreviewCalculator.ResistanceMultiplier(resistance));
                unblocked *= resistance.GetMultiplier();
            }

            hpLoss = ResolveHpLoss(state, unblocked, props, dealer, cardSource, pipelineType, libraryPipeline, trace);
            trace.Set(hpLoss, "");
            state.Details.Add(trace.RenderResult(IncomingDamagePreviewCalculator.HpLossText(hpLoss)));
        }

        state.Blocked += (int)blocked;
        state.HpLoss += hpLoss;
        state.Hp -= hpLoss;
        state.HpLostThisSide += hpLoss;
        state.HpLostSinceOwnerTurnStart += hpLoss;
        if (libraryPipeline)
        {
            ResolveChaos(state, amount, blockBeforeHit, props, dealer, pipelineType);
        }

        return new IncomingHitOutcome((int)blocked, hpLoss, (int)blocked + hpLoss);
    }

    private int ResolveHpLoss(
        IncomingDamageTargetState state,
        decimal unblocked,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type,
        bool libraryPipeline,
        DamagePreviewTrace trace)
    {
        Creature target = state.Creature;
        decimal hp = ModifyHpLost(target, unblocked, props, dealer, cardSource, type, libraryPipeline, afterRedirect: false);
        Creature hpTarget = libraryPipeline
            ? LibraryHooks.ModifyUnblockedDamageTarget(Combat, target, hp, props, dealer, type)
            : Hook.ModifyUnblockedDamageTarget(Combat, target, hp, props, dealer);
        // 模拟中已被击倒的伤害承担者（如奥斯提）不再分担后续伤害。
        if (hpTarget != target && GetRedirectHp(hpTarget) <= 0)
        {
            hpTarget = target;
        }

        hp = ModifyHpLost(hpTarget, hp, props, dealer, cardSource, type, libraryPipeline, afterRedirect: true);
        if (hpTarget == target)
        {
            int hpLoss = Math.Min(ToDamage(hp), Math.Max(0, state.Hp));
            ConsumeSlipperyAfterHit(target, hpLoss);
            return hpLoss;
        }

        int redirected = ToDamage(hp);
        int redirectHp = GetRedirectHp(hpTarget);
        _redirectHp[hpTarget] = Math.Max(0, redirectHp - redirected);
        int overkill = Math.Max(0, redirected - redirectHp);
        trace.Set(overkill, "");
        // 原版 CreatureCmd.Damage 先为承担者、再为原目标各生成一个 DamageResult（宠物吸收全部时原目标那份失血为 0），
        // 之后按结果逐个回调 AfterDamageReceived，回调的 target 是该结果的 Receiver。滑溜只在 target 是持有者且
        // 该结果失血 ≥ 1 时扣层，所以承担者与原目标各按自己那份失血消耗自己的滑溜；溢出部分已先经原目标的
        // AfterOsty 修正（滑溜在扣层前生效），扣层在本次命中的修正之后发生。
        int redirectLoss = Math.Min(redirected, redirectHp);
        if (overkill <= 0)
        {
            ConsumeSlipperyAfterHit(hpTarget, redirectLoss);
            ConsumeSlipperyAfterHit(target, 0);
            return 0;
        }

        decimal toTarget = ModifyHpLost(target, overkill, props, dealer, cardSource, type, libraryPipeline, afterRedirect: true);
        int targetLoss = Math.Min(ToDamage(toTarget), Math.Max(0, state.Hp));
        ConsumeSlipperyAfterHit(hpTarget, redirectLoss);
        ConsumeSlipperyAfterHit(target, targetLoss);
        return targetLoss;
    }

    /// <summary>按遗物实际收到的 Hook 输入记录候选条件，标记仅保存在当前模拟中。</summary>
    internal void RecordDearFamilySlipperyCandidate(
        BlackSwanDreamPageRelic relic, Creature target, decimal amount, bool afterRedirect)
    {
        if (afterRedirect)
        {
            if (_dearFamilySlipperyCandidates.Remove(relic) && target == relic.Owner.Creature)
            {
                _dearFamilySlipperyConsumptions.Add(relic);
            }

            return;
        }

        if (target == relic.Owner.Creature)
        {
            _dearFamilySlipperyConsumptions.Remove(relic);
        }

        _dearFamilySlipperyCandidates.Remove(relic);
        if (relic.Mode == BlackSwanDreamPageMode.DearFamily
            && target == relic.Owner.Creature
            && amount >= 1m
            && target.GetPower<SlipperyPower>() is { } slippery
            && GetTriggerCount(slippery) < slippery.Amount)
        {
            _dearFamilySlipperyCandidates.Add(relic);
        }
    }

    /// <summary>滑溜由受伤事件消耗，即使把 1 点伤害保持为 1 也会扣层，不能按修正器是否改变数值计数。</summary>
    private void ConsumeSlipperyAfterHit(Creature target, int unblockedDamage)
    {
        bool dearFamilyConsumes = _dearFamilySlipperyConsumptions.RemoveWhere(relic => relic.Owner.Creature == target) > 0;
        if (target.GetPower<SlipperyPower>() is not { } slippery
            || GetTriggerCount(slippery) >= slippery.Amount)
        {
            return;
        }

        if (unblockedDamage >= 1 || dearFamilyConsumes)
        {
            _triggerCounts[slippery] = GetTriggerCount(slippery) + 1;
        }
    }

    private decimal ModifyHpLost(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type,
        bool libraryPipeline,
        bool afterRedirect)
    {
        decimal result;
        IEnumerable<AbstractModel> modifiers;
        if (libraryPipeline)
        {
            result = afterRedirect
                ? LibraryHooks.ModifyHpLostAfterOsty(Run, Combat, target, amount, props, dealer, cardSource, out modifiers, type)
                : LibraryHooks.ModifyHpLostBeforeOsty(Run, Combat, target, amount, props, dealer, cardSource, out modifiers, type);
        }
        else
        {
            result = Hook.ModifyHpLost(Run, Combat, target, amount, props, dealer, cardSource,
                afterRedirect ? HpLossHookPhase.AfterOsty : HpLossHookPhase.BeforeOsty, out modifiers);
        }

        // 数值修正后置回调只通知改变了数值的模型；滑溜另由受伤事件消耗，避免重复扣层。
        foreach (AbstractModel modifier in modifiers)
        {
            if (modifier is SlipperyPower)
            {
                continue;
            }

            _triggerCounts[modifier] = GetTriggerCount(modifier) + 1;
        }

        return result;
    }

    /// <summary>原版攻击命中 Library 生物后，以本次基础伤害扣除命中前格挡进入混乱伤害；混乱后抗性视为致命。</summary>
    private void ResolveChaos(
        IncomingDamageTargetState state,
        decimal amount,
        int blockBeforeHit,
        ValueProp props,
        Creature? dealer,
        LibraryDamageType type)
    {
        if (state.Creature is not LibraryCreature { HasChaoResistance: true } target
            || state.IsChaoed
            || state.ChaoValue <= 0
            || state.IsDown)
        {
            return;
        }

        decimal chaos = LibraryHooks.ModifyChaoDamage(Run, Combat, target, dealer, Math.Max(0m, amount - blockBeforeHit),
            props, null, null, ModifyChaoDamageHookType.All, CardPreviewMode.None, out _, type);
        if (ResistancePreview.ShouldApplyResistance(props, type))
        {
            chaos *= target.GetChaosResistanceLevel(type).GetMultiplier();
        }

        state.ChaoValue = Math.Max(0, state.ChaoValue - (int)Math.Max(0m, decimal.Truncate(chaos)));
        if (state.ChaoValue == 0)
        {
            state.IsChaoed = true;
            // 本次命中使其混乱：在该命中条目末尾标记混乱图标。
            state.Details[^1] += $" {IncomingDamagePreviewCalculator.StaggerIcon}";
        }
    }

    /// <summary>腐蚀类能力在强化攻击命中后追加伤害；追加伤害为无强化伤害，不会再次触发。</summary>
    internal void ResolveCorrosionFollowUps(
        IncomingDamageTargetState state,
        IncomingHitOutcome outcome,
        ValueProp props,
        Creature? attacker)
    {
        foreach (ICorrosionFollowUpPower corrosion in state.Creature.Powers.OfType<ICorrosionFollowUpPower>().ToArray())
        {
            int stacks = state.CorrosionStacks.TryGetValue(corrosion, out int remaining)
                ? remaining
                : corrosion.Amount;
            if (state.IsDown || !CorrosionFollowUpRules.TriggersOnHit(stacks, outcome.TotalDamage, props))
            {
                continue;
            }

            using (corrosion.EnterDamageSourceScope())
            {
                ResolveHit(
                    state,
                    stacks,
                    CorrosionFollowUpRules.DamageProps,
                    corrosion.GetHitFollowUpDealer(attacker),
                    null,
                    LibraryDamageType.None,
                    DamagePreviewTrace.Name(corrosion),
                    applyDamageHooks: true);
            }
        }
    }

    private int GetRedirectHp(Creature creature) =>
        _redirectHp.TryGetValue(creature, out int hp) ? hp : Math.Max(0, creature.IsAlive ? creature.CurrentHp : 0);

    private static int ToDamage(decimal amount) => (int)Math.Clamp(amount, 0m, 999999999m);

    private sealed class Scope(IncomingDamageSimulation? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _current = previous;
        }
    }
}
