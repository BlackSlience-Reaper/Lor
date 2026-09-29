using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.monsters.PhilosophyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.PhilosophyFloorLiberation;

/// <summary>
/// 暮光审判无视能力：审判伤害的三个修正钩子（ModifyDamage、ModifyHpLost、ModifyUnblockedDamageTarget）里，
/// HookListenerFilterPatch 从监听者中剔除全部 PowerModel。作用域跟着审判伤害的 CreatureCmd.Damage 走，
/// 但这次调用里的 AfterDamageGiven/AfterDamageReceived 等回调还会引发别的伤害（反伤、受击触发），
/// 所以修正钩子只在伤害来源正是审判者时才进入剔除作用域，其他伤害照常计算能力。
/// </summary>
internal static class PhilosophyFloorTwilightJudgmentPowerBypassContext
{
    private static readonly AsyncLocal<Creature?> JudgmentDealer = new();
    private static readonly AsyncLocal<int> PowerModifierHookDepth = new();

    internal static bool ShouldBypassPowerModifiers =>
        JudgmentDealer.Value != null && PowerModifierHookDepth.Value > 0;

    internal static IDisposable EnterJudgmentDamage(Creature dealer)
    {
        Creature? previous = JudgmentDealer.Value;
        JudgmentDealer.Value = dealer;
        return new Scope(() => JudgmentDealer.Value = previous);
    }

    internal static IDisposable? EnterPowerModifierHook(Creature? dealer)
    {
        if (dealer == null || JudgmentDealer.Value != dealer)
        {
            return null;
        }

        PowerModifierHookDepth.Value++;
        return new Scope(() => PowerModifierHookDepth.Value = Math.Max(0, PowerModifierHookDepth.Value - 1));
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            restore();
        }
    }
}

internal static class PhilosophyFloorTwilightPowerController
{
    internal static async Task EnsureBossPowers(PhilosophyFloorTwilight boss)
    {
        await PowerCmdCompat.Ensure<PhilosophyFloorTwilightBlackMonsterPower>(
            boss.Creature);
        await PowerCmdCompat.Ensure<PhilosophyFloorTwilightThreeBirdsPower>(
            boss.Creature);
        await PowerCmdCompat.Ensure<PhilosophyFloorTwilightBrokenEggPower>(
            boss.Creature);
        await PowerCmdCompat.Ensure<PhilosophyFloorTwilightLongArmsControllerPower>(
            boss.Creature);
        await PowerCmdCompat.Ensure<PhilosophyFloorTwilightSmallBeakControllerPower>(
            boss.Creature);
        await SyncPeacePower(boss);
    }

    internal static async Task SyncPeacePower(
        PhilosophyFloorTwilight boss)
    {
        Type? requiredType = boss.AliveEggCount switch
        {
            3 => typeof(PhilosophyFloorTwilightPeace75Power),
            2 => typeof(PhilosophyFloorTwilightPeace50Power),
            1 => typeof(PhilosophyFloorTwilightPeace25Power),
            _ => null
        };

        PowerModel[] peacePowers = boss.Creature.Powers
            .Where(static power =>
                power is PhilosophyFloorTwilightPeacePowerBase)
            .ToArray();
        foreach (PowerModel power in peacePowers)
        {
            if (power.GetType() != requiredType)
            {
                await PowerCmd.Remove(power);
            }
        }

        if (requiredType == typeof(PhilosophyFloorTwilightPeace75Power))
        {
            await PowerCmdCompat.Ensure<PhilosophyFloorTwilightPeace75Power>(
                boss.Creature);
        }
        else if (requiredType == typeof(PhilosophyFloorTwilightPeace50Power))
        {
            await PowerCmdCompat.Ensure<PhilosophyFloorTwilightPeace50Power>(
                boss.Creature);
        }
        else if (requiredType == typeof(PhilosophyFloorTwilightPeace25Power))
        {
            await PowerCmdCompat.Ensure<PhilosophyFloorTwilightPeace25Power>(
                boss.Creature);
        }
    }
}

public sealed class PhilosophyFloorTwilightBlackMonsterPower :
    LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "PHILOSOPHY_FLOOR_TWILIGHT_BLACK_MONSTER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "CycleTurns",
            PhilosophyFloorTwilight.ModeCycleLength)
    ];
}

public sealed class PhilosophyFloorTwilightThreeBirdsPower :
    LibraryOfRuinaPowerModel
{
    private const string PowerLocKey =
        "PHILOSOPHY_FLOOR_TWILIGHT_THREE_BIRDS_POWER";

    protected override string LegacyPowerId => PowerLocKey;

    protected override string SmartDescriptionLocKey
    {
        get
        {
            if (!IsMutable
                || Owner?.Monster is not PhilosophyFloorTwilight boss)
            {
                return $"{PowerLocKey}.smartDescription";
            }

            return ResolveSmartDescriptionLocKey(boss.ActiveEgg);
        }
    }

    internal static string ResolveSmartDescriptionLocKey(
        PhilosophyFloorTwilightEgg activeEgg) => activeEgg switch
    {
        PhilosophyFloorTwilightEgg.BigEyes =>
            $"{PowerLocKey}.smartDescription.bigEyes",
        PhilosophyFloorTwilightEgg.SmallBeak =>
            $"{PowerLocKey}.smartDescription.smallBeak",
        PhilosophyFloorTwilightEgg.LongArms =>
            $"{PowerLocKey}.smartDescription.longArms",
        _ => $"{PowerLocKey}.smartDescription.none"
    };

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "EggCycleTurns",
            PhilosophyFloorTwilight.EggScheduleCycleLength),
        new DynamicVar(
            "EggStageTurns",
            PhilosophyFloorTwilight.EggScheduleStageLength),
        new DynamicVar(
            "CardsPlayed",
            PhilosophyFloorTwilightSmallBeakControllerPower
                .CardsPlayedPerTrigger),
        new DynamicVar(
            "CardsAffected",
            PhilosophyFloorTwilightSmallBeakControllerPower
                .CardsAffectedPerTrigger),
        new EnergyVar(
            "CostIncrease",
            PhilosophyFloorTwilightSmallBeakControllerPower
                .CostIncreaseThisTurn),
        new PowerVar<LibraryStrongPower>(
            "Strong",
            PhilosophyFloorTwilightLongArmsControllerPower
                .StrongPerDebuff)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];
}

public sealed class PhilosophyFloorTwilightBrokenEggPower :
    LibraryOfRuinaPowerModel
{
    // 第二进阶10：小鸟蛋破碎后，玩家卡牌的最低能量费用。
    internal const int BrokenSmallBeakMinimumCost = 2;

    // 第二进阶10：长臂蛋破碎后，每回合最多反弹的负面效果次数。
    internal const int BrokenLongArmsReflectionLimit = 3;

    public int ReflectionRound { get; set; }

    public int ReflectionsThisRound { get; set; }

    protected override string LegacyPowerId =>
        "PHILOSOPHY_FLOOR_TWILIGHT_BROKEN_EGG_POWER";

    protected override string SmartDescriptionLocKey =>
        IsMutable && Owner.Monster is PhilosophyFloorTwilight { HasEnhancedBrokenEggs: true }
            ? $"{LegacyPowerId}.smartDescription.secondAscension"
            : $"{LegacyPowerId}.smartDescription";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLossPercent", PhilosophyFloorTwilight.EggBreakHpLossPercent),
        new DynamicVar("PowerGain", PhilosophyFloorTwilight.PermanentPowerGainPerEgg),
        new EnergyVar("MinimumCost", BrokenSmallBeakMinimumCost),
        new DynamicVar("ReflectionLimit", BrokenLongArmsReflectionLimit)
    ];

    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Owner.IsAlive
            && Owner.Monster is PhilosophyFloorTwilight { HasEnhancedBrokenEggs: true } boss
            && !boss.IsEggAlive(PhilosophyFloorTwilightEgg.SmallBeak)
            && card.Owner.Creature.CombatState == Owner.CombatState
            && !card.EnergyCost.CostsX
            && originalCost >= 0m)
        {
            modifiedCost = Math.Max(originalCost, BrokenSmallBeakMinimumCost);
        }

        return modifiedCost != originalCost;
    }

    internal bool TryConsumeReflection(PowerModel power, decimal amount, Creature? applier)
    {
        if (!Owner.IsAlive
            || applier == null
            || applier == Owner
            || !applier.CanReceivePowers
            || CombatManager.Instance.IsEnding
            || amount == 0m
            || power.GetTypeForAmount(amount) != PowerType.Debuff
            || Owner.Monster is not PhilosophyFloorTwilight { HasEnhancedBrokenEggs: true } boss
            || boss.IsEggAlive(PhilosophyFloorTwilightEgg.LongArms))
        {
            return false;
        }

        int round = Owner.CombatState!.RoundNumber;
        if (ReflectionRound != round)
        {
            ReflectionRound = round;
            ReflectionsThisRound = 0;
        }

        if (ReflectionsThisRound >= BrokenLongArmsReflectionLimit)
        {
            return false;
        }

        ReflectionsThisRound++;
        Flash();
        return true;
    }
}

// 在实际 PowerCmd 入口转移效果，纯计算 Hook 不消耗反弹次数。
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply),
    [typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature),
        typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)])]
internal static class PhilosophyFloorTwilightReflectAppliedPowerPatch
{
    private static void Prefix(PowerModel power, ref Creature target, decimal amount, ref Creature? applier)
    {
        if (target.GetPower<PhilosophyFloorTwilightBrokenEggPower>() is { } reflection
            && reflection.TryConsumeReflection(power, amount, applier))
        {
            Creature originalTarget = target;
            target = applier!;
            applier = originalTarget;
        }
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount))]
[LibraryPatch(Reason = "原版 Hook 只能修改能力数值，不能改目标；为了在命令入口把叠加型负面效果反弹给施加者，只对本模组强化蛋暮光的碎蛋能力生效。")]
internal static class PhilosophyFloorTwilightReflectStackedPowerPatch
{
    private static bool Prefix(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal offset,
        Creature? applier,
        CardModel? cardSource,
        bool silent,
        ref Task<int> __result)
    {
        if (power.Owner.GetPower<PhilosophyFloorTwilightBrokenEggPower>() is not { } reflection
            || !reflection.TryConsumeReflection(power, offset, applier))
        {
            return true;
        }

        __result = Reflect(choiceContext, power, offset, applier!, cardSource, silent);
        return false;
    }

    private static async Task<int> Reflect(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal offset,
        Creature applier,
        CardModel? cardSource,
        bool silent)
    {
        await PowerCmd.Apply(choiceContext, (PowerModel)ModelDb.GetById<PowerModel>(power.Id).MutableClone(),
            applier, offset, power.Owner, cardSource, silent);
        return power.Amount;
    }
}

public sealed class PhilosophyFloorTwilightLongArmsControllerPower :
    LibraryOfRuinaPowerModel
{
    public const int StrongPerDebuff = 1;

    protected override string LegacyPowerId =>
        "PHILOSOPHY_FLOOR_TWILIGHT_LONG_ARMS_CONTROLLER_POWER";

    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<LibraryStrongPower>("Strong", StrongPerDebuff)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? _,
        out decimal modifiedAmount)
    {
        if (target == Owner
            && amount != 0m
            && canonicalPower.IsVisible
            && canonicalPower.GetTypeForAmount(amount) == PowerType.Debuff
            && Owner.Monster is PhilosophyFloorTwilight boss
            && boss.IsEggActive(PhilosophyFloorTwilightEgg.LongArms))
        {
            modifiedAmount = 0m;
            return true;
        }

        modifiedAmount = amount;
        return false;
    }

    public override async Task AfterModifyingPowerAmountReceived(
        PowerModel _)
    {
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            StrongPerDebuff,
            0,
            true,
            Owner,
            null);
    }
}

public sealed class PhilosophyFloorTwilightSmallBeakControllerPower :
    LibraryOfRuinaPowerModel
{
    internal const int CardsPlayedPerTrigger = 1;
    internal const int CardsAffectedPerTrigger = 1;
    internal const int CostIncreaseThisTurn = 2;

    private sealed class Data
    {
        public Dictionary<Player, CardModel> LastSelectedCards { get; } = [];
    }

    protected override string LegacyPowerId =>
        "PHILOSOPHY_FLOOR_TWILIGHT_SMALL_BEAK_CONTROLLER_POWER";

    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("CardsPlayed", CardsPlayedPerTrigger),
        new DynamicVar("CardsAffected", CardsAffectedPerTrigger),
        new EnergyVar("CostIncrease", CostIncreaseThisTurn)
    ];

    protected override object InitInternalData() => new Data();

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        CombatStateLike? combatState = Owner.CombatState;
        if (combatState == null
            || Owner.IsDead
            || Owner.Monster is not PhilosophyFloorTwilight boss
            || !boss.IsEggActive(PhilosophyFloorTwilightEgg.SmallBeak))
        {
            return Task.CompletedTask;
        }

        Player player = cardPlay.Card.Owner;
        if (!player.Creature.IsAlive)
        {
            return Task.CompletedTask;
        }

        CardModel[] candidates =
            player.PlayerCombatState?.Hand.Cards
                .Where(static card =>
                    !card.EnergyCost.CostsX
                    && card.EnergyCost.GetWithModifiers(
                        CostModifiers.None) >= 0)
                .ToArray() ?? [];
        Data data = GetInternalData<Data>();
        data.LastSelectedCards.TryGetValue(player, out CardModel? lastCard);
        CardModel[] selectionPool = candidates.Length > 1 && lastCard != null
            ? candidates
                .Where(card => !ReferenceEquals(card, lastCard))
                .ToArray()
            : candidates;
        Rng rng = player.RunState.Rng.CombatEnergyCosts;
        CardModel? selectedCard = rng.NextItem(selectionPool);
        if (selectedCard != null)
        {
            data.LastSelectedCards[player] = selectedCard;
            selectedCard.EnergyCost.AddThisTurn(CostIncreaseThisTurn);
            selectedCard.InvokeEnergyCostChanged();
        }

        return Task.CompletedTask;
    }
}

public sealed class PhilosophyFloorTwilightSinPower :
    LibraryOfRuinaPowerModel
{
    private const int CardsPlayedPerTrigger = 1;
    private const int SinLossPerTrigger = 1;

    protected override string LegacyPowerId =>
        "PHILOSOPHY_FLOOR_TWILIGHT_SIN_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("CardsPlayed", CardsPlayedPerTrigger),
        new DynamicVar("SinLoss", SinLossPerTrigger)
    ];

    public override async Task AfterCardPlayed(
        PlayerChoiceContext context,
        CardPlay cardPlay)
    {
        if (Owner.IsDead
            || Amount <= 0
            || cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        decimal hpLoss = Amount;
        Flash();
        await CreatureCmd.Damage(
            context,
            Owner,
            hpLoss,
            ValueProp.Unpowered,
            null,
            null);
        await PowerCmdCompat.ModifyAmount(
            this,
            -SinLossPerTrigger,
            Owner,
            cardPlay.Card,
            silent: true);
        if (Amount <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}

public sealed class PhilosophyFloorTwilightFearPower :
    LibraryOfRuinaPowerModel
{
    private const int ParalysisStacks = 4;
    private const int ParalysisTurns = 1;

    protected override string LegacyPowerId =>
        "PHILOSOPHY_FLOOR_TWILIGHT_FEAR_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool AllowNegative => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Paralysis", ParalysisStacks),
        new DynamicVar("Turns", ParalysisTurns)
    ];

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (Owner.IsDead || player.Creature != Owner)
        {
            return;
        }

        Flash();
        LibraryOfRuinaParalysisPower? paralysis =
            await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaParalysisPower>(
                choiceContext,
                Owner,
                ParalysisStacks,
                Owner,
                null);
        paralysis?.SetTurnsRemaining(ParalysisTurns);
    }
}

public abstract class PhilosophyFloorTwilightPeacePowerBase :
    LibraryOfRuinaPowerModel, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    internal const int ThreeEggsMinimumHpPercent = 75;
    internal const int TwoEggsMinimumHpPercent = 50;
    internal const int OneEggMinimumHpPercent = 25;

    protected abstract int MinimumHpPercent { get; }

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("MinimumHpPercent", MinimumHpPercent)];

    internal bool IsHealthBarLockActive => Owner.CurrentHp <= MinimumHp;

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m)
        {
            return amount;
        }

        decimal maxLoss = Owner.CurrentHp - MinimumHp;
        return maxLoss <= 0m ? 0m : Math.Min(amount, maxLoss);
    }

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta)
    {
        if (creature == Owner
            && delta < 0m
            && Owner.CurrentHp < MinimumHp)
        {
            await CreatureCmd.SetCurrentHp(Owner, MinimumHp);
        }
    }

    public override bool ShouldDie(Creature creature) =>
        creature != Owner || Owner.CurrentHp > MinimumHp;

    public override Task AfterPreventingDeath(Creature creature) =>
        creature == Owner
            ? CreatureCmd.SetCurrentHp(Owner, MinimumHp)
            : Task.CompletedTask;

    private decimal MinimumHp => Math.Max(
        1m,
        Math.Ceiling(Owner.MaxHp * MinimumHpPercent / 100m));
}

public sealed class PhilosophyFloorTwilightPeace75Power :
    PhilosophyFloorTwilightPeacePowerBase
{
    protected override string LegacyPowerId =>
        "PHILOSOPHY_FLOOR_TWILIGHT_PEACE_75_POWER";

    protected override int MinimumHpPercent => ThreeEggsMinimumHpPercent;
}

public sealed class PhilosophyFloorTwilightPeace50Power :
    PhilosophyFloorTwilightPeacePowerBase
{
    protected override string LegacyPowerId =>
        "PHILOSOPHY_FLOOR_TWILIGHT_PEACE_50_POWER";

    protected override int MinimumHpPercent => TwoEggsMinimumHpPercent;
}

public sealed class PhilosophyFloorTwilightPeace25Power :
    PhilosophyFloorTwilightPeacePowerBase
{
    protected override string LegacyPowerId =>
        "PHILOSOPHY_FLOOR_TWILIGHT_PEACE_25_POWER";

    protected override int MinimumHpPercent => OneEggMinimumHpPercent;
}

/// <summary>“大眼”蛋激活时暂停的能力；监听者过滤在 <see cref="LibraryOfRuina.patches.dispatch.HookListenerFilterPatch"/>。</summary>
internal static class PhilosophyFloorTwilightBigEyesHookSuspension
{
    internal static bool ShouldSuspendPower(PowerModel power) =>
        power.TypeForCurrentAmount == PowerType.Buff
        && power is not ArtifactPower;
}

// 以下三个补丁只标出“正在审判伤害的修正钩子里”，剔除由 HookListenerFilterPatch 完成。原版 IterateHookListeners
// 不区分调用它的钩子，没有别的办法只在这三个钩子里剔除能力；任何模型覆写都拿不走其他监听者的修正。
// 基础库的解析前缀跳过原方法时这些前缀不执行（参数是引用类型），原版不再遍历监听者，Finalizer 对空状态无操作。
// 之后仍会遍历监听者的只有 ModifyHpLost 上的锁血后缀（VanillaFinalHpLossClampPatch），它自己建立同样的作用域。
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
[LibraryPatch(Reason = "原版 IterateHookListeners 不区分调用它的钩子；暮光审判只在审判者造成的伤害的修正钩子里剔除全部能力，其余调用不进作用域。")]
internal static class PhilosophyFloorTwilightJudgmentModifyDamagePatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature? dealer, out IDisposable? __state)
    {
        __state = PhilosophyFloorTwilightJudgmentPowerBypassContext
            .EnterPowerModifierHook(dealer);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(
        Exception? __exception,
        IDisposable? __state)
    {
        __state?.Dispose();
        return __exception;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
[LibraryPatch(Reason = "同 PhilosophyFloorTwilightJudgmentModifyDamagePatch；Finalizer 排在锁血后缀之后，锁血遍历同样剔除能力。")]
internal static class PhilosophyFloorTwilightJudgmentModifyHpLostPatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature? dealer, out IDisposable? __state)
    {
        __state = PhilosophyFloorTwilightJudgmentPowerBypassContext
            .EnterPowerModifierHook(dealer);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(
        Exception? __exception,
        IDisposable? __state)
    {
        __state?.Dispose();
        return __exception;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyUnblockedDamageTarget))]
[LibraryPatch(Reason = "同 PhilosophyFloorTwilightJudgmentModifyDamagePatch；审判伤害不被能力转移到其他目标。")]
internal static class PhilosophyFloorTwilightJudgmentDamageRedirectPatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature? dealer, out IDisposable? __state)
    {
        __state = PhilosophyFloorTwilightJudgmentPowerBypassContext
            .EnterPowerModifierHook(dealer);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(
        Exception? __exception,
        IDisposable? __state)
    {
        __state?.Dispose();
        return __exception;
    }
}
