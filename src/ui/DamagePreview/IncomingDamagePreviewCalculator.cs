using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryLib.Hooks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.ui.DamagePreview;

/// <summary>一名受击者在下次玩家行动前的受伤预览。</summary>
internal sealed record IncomingDamagePreviewResult(
    int Blocked,
    int HpLoss,
    bool IsLethal,
    string Summary,
    string Details);

/// <summary>
/// 按实际结算顺序模拟本地玩家回合结束与随后的敌方回合：回合结束格挡、自伤与持续伤害，
/// 再按敌人行动顺序逐次命中。伤害、格挡与失去生命值全部经由原版或基础库 Hook，
/// 监听者的扣血逻辑由隔离的 Hook 解释器读取，未知调用标记为部分预测。
/// 格挡、生命、层数与钩子字段只在模拟对象内推进。
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
            Creature[] playerParticipants = combat.Players.Select(static player => player.Creature).ToArray();
            if (localPlayer != null)
            {
                simulation.SimulateTurnEnd(CombatSide.Player, playerParticipants, before: true);
                foreach (Creature player in playerParticipants)
                {
                    simulation.SimulateHand(player);
                }

                simulation.SimulateTurnEnd(CombatSide.Player, playerParticipants, before: false);
            }

            simulation.BeginEnemyTurn();
            Creature[] enemyParticipants = combat.Enemies.ToArray();
            simulation.SimulateTurnStart(CombatSide.Enemy, enemyParticipants);
            SimulateEnemyAttacks(simulation);
            simulation.SimulateTurnEnd(CombatSide.Enemy, enemyParticipants, before: true);
            simulation.SimulateTurnEnd(CombatSide.Enemy, enemyParticipants, before: false);
            simulation.SimulateTurnStart(CombatSide.Player, combat.GetCreaturesOnSide(CombatSide.Player));
        }

        var results = new Dictionary<Creature, IncomingDamagePreviewResult>();
        foreach (IncomingDamageTargetState state in simulation.States)
        {
            if (targets.Contains(state.Creature) && (state.HasIncomingDamage || simulation.IsPartial))
            {
                results[state.Creature] = BuildResult(state, simulation.IsPartial);
            }
        }

        return results;
    }

    private static IncomingDamagePreviewResult BuildResult(IncomingDamageTargetState state, bool isPartial)
    {
        bool isLethal = !isPartial && state.Hp <= 0;
        string blocked = DamagePreviewTrace.Tint(state.Blocked.ToString(), StsColors.blue);
        string hpLoss = HpLossText(state.HpLoss);
        string lethal = isLethal ? LethalIcon : "";
        string partial = isPartial ? " ?" : "";
        string summary = $"{BlockIcon}{blocked}\n{HeartIcon}{hpLoss}{lethal}{partial}";
        string total = $"= {BlockIcon}{blocked} {HeartIcon}{hpLoss}{lethal}{partial}";
        string details = string.Join("\n", state.Details.Append(total));
        return new IncomingDamagePreviewResult(state.Blocked, state.HpLoss, isLethal, summary, details);
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

                simulation.HookReader.TryExecute(enemy.Monster,
                    AccessTools.Method(typeof(IncomingDamagePreviewCalculator), nameof(SimulateAttack)),
                    () => SimulateAttack(simulation, enemy, attack, ordinaryTargets, targets));
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
            if (simulation.TryGetState(enemy, out IncomingDamageTargetState? enemyState) && enemyState!.IsDown)
            {
                return;
            }

            foreach (Creature target in targets)
            {
                Creature receiver = (Creature)simulation.EvaluateHook(typeof(LibraryHooks),
                    nameof(LibraryHooks.ModifyDamageTarget), simulation.Combat, target, amount, ValueProp.Move, enemy, type)!;
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
                simulation.DispatchDamageCallbacks(state, outcome, ValueProp.Move, enemy, null);
            }
        }
    }
}

internal readonly record struct IncomingHitOutcome(int Blocked, int HpLoss, int TotalDamage,
    IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.DamageResult>? RedirectedResults = null);

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

    internal List<string> Details { get; } = [];

    internal IncomingDamageTargetState Snapshot()
    {
        var snapshot = new IncomingDamageTargetState(Creature);
        snapshot.Restore(this);
        return snapshot;
    }

    internal void Restore(IncomingDamageTargetState snapshot)
    {
        Block = snapshot.Block;
        Hp = snapshot.Hp;
        ChaoValue = snapshot.ChaoValue;
        IsChaoed = snapshot.IsChaoed;
        Blocked = snapshot.Blocked;
        HpLoss = snapshot.HpLoss;
        HpLostThisSide = snapshot.HpLostThisSide;
        HpLostSinceOwnerTurnStart = snapshot.HpLostSinceOwnerTurnStart;
        HasIncomingDamage = snapshot.HasIncomingDamage;
        Details.Clear();
        Details.AddRange(snapshot.Details);
    }
}

/// <summary>
/// 一次预览的模拟上下文。<see cref="Current"/> 只在同步计算期间存在，
/// 实际监听者的字段与次数进度由 <see cref="IncomingDamagePreviewHookReader"/> 独立保存。
/// </summary>
internal sealed partial class IncomingDamageSimulation
{
    [ThreadStatic]
    private static IncomingDamageSimulation? _current;

    private readonly Dictionary<Creature, IncomingDamageTargetState> _states = [];
    private readonly Dictionary<Creature, int> _redirectHp = [];

    internal IncomingDamageSimulation(CombatState combat, IReadOnlyList<Creature> targets)
    {
        Combat = combat;
        Run = combat.RunState;
        foreach (Creature target in combat.Creatures.Concat(targets).Distinct())
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

    internal void GainBlock(IncomingDamageTargetState state, BlockVar block, string source)
    {
        GainBlock(state, block.BaseValue, block.Props, source);
    }

    /// <summary>与 CreatureCmd.GainBlock 相同：经格挡 Hook 修正后按整数累加。</summary>
    internal void GainBlock(IncomingDamageTargetState state, decimal amount, ValueProp props, string source)
    {
        decimal modified = Math.Max(0m, EvaluateDecimalHook(typeof(Hook), nameof(Hook.ModifyBlock),
            Combat, state.Creature, amount, props, null, null, OutputReference(_ => { })));
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
        bool applyDamageHooks,
        bool forceLibraryPipeline = false,
        bool resolveChaos = true)
    {
        state.HasIncomingDamage = true;
        Creature target = state.Creature;
        // 强化攻击命中非玩家的 Library 生物时由基础库结算抗性与混乱；其余伤害走原版管线。
        bool libraryPipeline = target is LibraryCreature
            && (forceLibraryPipeline || !target.IsPlayer && ValuePropCompat.IsPoweredAttack(props));
        LibraryDamageType pipelineType = libraryPipeline ? type : LibraryDamageType.None;
        IncomingDamageTargetState blockState = target.PetOwner != null
            && TryGetState(target.PetOwner.Creature, out IncomingDamageTargetState? parentState)
            ? parentState! : state;
        int blockBeforeHit = blockState.Block;
        decimal blocked;
        int hpLoss;
        IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.DamageResult> redirectedResults;
        using (var trace = new DamagePreviewTrace(amount, source))
        {
            decimal damage = amount;
            if (applyDamageHooks)
            {
                IEnumerable<AbstractModel> damageModifiers = [];
                var modifierOutput = OutputReference(value => damageModifiers =
                    ((System.Collections.IEnumerable?)value)?.Cast<AbstractModel>() ?? []);
                damage = libraryPipeline
                    ? EvaluateDecimalHook(typeof(LibraryHooks), nameof(LibraryHooks.ModifyDamage), Run, Combat,
                        target, dealer, amount, props, cardSource, null, ModifyDamageHookType.All, CardPreviewMode.None,
                        modifierOutput, pipelineType)
                    : EvaluateDecimalHook(typeof(Hook), nameof(Hook.ModifyDamage), GameApi.ModifyDamageHookArguments(
                        Run, Combat, target, dealer, amount, props, cardSource, null, ModifyDamageHookType.All,
                        CardPreviewMode.None, modifierOutput));
                trace.Set(damage, "");
                foreach (AbstractModel modifier in damageModifiers)
                {
                    HookReader.TryInvoke(modifier,
                        AccessTools.Method(modifier.GetType(), nameof(AbstractModel.AfterModifyingDamageAmount)),
                        [cardSource]);
                }
            }

            damage = Math.Max(0m, damage);
            SimulateHooks(nameof(AbstractModel.BeforeDamageReceived), null, target, damage, props, dealer, cardSource);
            blocked = props.HasFlag(ValueProp.Unblockable) ? 0m : Math.Min(blockState.Block, damage);
            if (blocked > 0m)
            {
                trace.Add(-blocked, IncomingDamagePreviewCalculator.BlockIcon);
            }

            blockState.Block = Math.Max(0, blockState.Block - (int)blocked);
            state.Block = blockState.Block;
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

            hpLoss = ResolveHpLoss(state, unblocked, props, dealer, cardSource, pipelineType, libraryPipeline, trace,
                out redirectedResults);
            trace.Set(hpLoss, "");
            state.Details.Add(trace.RenderResult(IncomingDamagePreviewCalculator.HpLossText(hpLoss)));
        }

        state.Blocked += (int)blocked;
        state.HpLoss += hpLoss;
        state.Hp -= hpLoss;
        state.HpLostThisSide += hpLoss;
        state.HpLostSinceOwnerTurnStart += hpLoss;
        if (libraryPipeline && resolveChaos)
        {
            ResolveChaos(state, amount, blockBeforeHit, props, dealer, pipelineType);
        }

        return new IncomingHitOutcome((int)blocked, hpLoss, (int)blocked + hpLoss, redirectedResults);
    }

    private int ResolveHpLoss(
        IncomingDamageTargetState state,
        decimal unblocked,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type,
        bool libraryPipeline,
        DamagePreviewTrace trace,
        out IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.DamageResult> redirectedResults)
    {
        redirectedResults = [];
        Creature target = state.Creature;
        decimal hp = ModifyHpLost(target, unblocked, props, dealer, cardSource, type, libraryPipeline, afterRedirect: false);
        Creature hpTarget = (Creature)(libraryPipeline
            ? EvaluateHook(typeof(LibraryHooks), nameof(LibraryHooks.ModifyUnblockedDamageTarget),
                Combat, target, hp, props, dealer, type)
            : EvaluateHook(typeof(Hook), nameof(Hook.ModifyUnblockedDamageTarget), Combat, target, hp, props, dealer))!;
        // 模拟中已被击倒的伤害承担者（如奥斯提）不再分担后续伤害。
        if (hpTarget != target && GetRedirectHp(hpTarget) <= 0)
        {
            hpTarget = target;
        }

        hp = ModifyHpLost(hpTarget, hp, props, dealer, cardSource, type, libraryPipeline, afterRedirect: true);
        if (hpTarget == target)
        {
            int hpLoss = Math.Min(ToDamage(hp), Math.Max(0, state.Hp));
            return hpLoss;
        }

        int redirected = ToDamage(hp);
        int redirectHp = GetRedirectHp(hpTarget);
        _redirectHp[hpTarget] = Math.Max(0, redirectHp - redirected);
        int overkill = Math.Max(0, redirected - redirectHp);
        trace.Set(overkill, "");
        // 承担者先结算自己的失血，原目标再处理溢出；各自的受伤回调复用对应 DamageResult。
        int redirectLoss = Math.Min(redirected, redirectHp);
        if (TryGetState(hpTarget, out IncomingDamageTargetState? redirectState))
        {
            redirectState!.Hp = Math.Max(0, redirectHp - redirectLoss);
            redirectState.HpLoss += redirectLoss;
            redirectState.HpLostThisSide += redirectLoss;
            redirectState.HpLostSinceOwnerTurnStart += redirectLoss;
        }

        redirectedResults = [new MegaCrit.Sts2.Core.Entities.Creatures.DamageResult(hpTarget, props)
        {
            UnblockedDamage = redirectLoss,
            OverkillDamage = overkill,
            WasTargetKilled = redirectLoss >= redirectHp
        }];
        if (overkill <= 0)
        {
            return 0;
        }

        decimal toTarget = ModifyHpLost(target, overkill, props, dealer, cardSource, type, libraryPipeline, afterRedirect: true);
        int targetLoss = Math.Min(ToDamage(toTarget), Math.Max(0, state.Hp));
        return targetLoss;
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
        IEnumerable<AbstractModel> modifiers = [];
        var output = OutputReference(value => modifiers = ((System.Collections.IEnumerable?)value)?.Cast<AbstractModel>() ?? []);
        if (libraryPipeline)
        {
            result = afterRedirect
                ? EvaluateDecimalHook(typeof(LibraryHooks), nameof(LibraryHooks.ModifyHpLostAfterOsty),
                    Run, Combat, target, amount, props, dealer, cardSource, output, type)
                : EvaluateDecimalHook(typeof(LibraryHooks), nameof(LibraryHooks.ModifyHpLostBeforeOsty),
                    Run, Combat, target, amount, props, dealer, cardSource, output, type);
        }
        else
        {
            result = EvaluateDecimalHook(typeof(Hook), nameof(Hook.ModifyHpLost), Run, Combat, target, amount,
                props, dealer, cardSource, afterRedirect ? HpLossHookPhase.AfterOsty : HpLossHookPhase.BeforeOsty, output);
        }

        // 数值修正后置回调只通知改变了数值的监听者，次数消耗由真实回调的 IL 推进。
        foreach (AbstractModel modifier in modifiers)
        {
            string method = afterRedirect ? nameof(AbstractModel.AfterModifyingHpLostAfterOsty)
                : nameof(AbstractModel.AfterModifyingHpLostBeforeOsty);
            HookReader.TryInvoke(modifier, AccessTools.Method(modifier.GetType(), method));
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

        decimal chaos = EvaluateDecimalHook(typeof(LibraryHooks), nameof(LibraryHooks.ModifyChaoDamage), Run, Combat,
            target, dealer, Math.Max(0m, amount - blockBeforeHit), props, null, null,
            ModifyChaoDamageHookType.All, CardPreviewMode.None, OutputReference(_ => { }), type);
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

    private int GetRedirectHp(Creature creature) => TryGetState(creature, out IncomingDamageTargetState? state)
        ? Math.Max(0, state!.Hp)
        : _redirectHp.GetValueOrDefault(creature, Math.Max(0, creature.CurrentHp));

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
