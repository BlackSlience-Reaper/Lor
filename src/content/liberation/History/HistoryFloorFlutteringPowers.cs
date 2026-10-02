using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.History;

public sealed class FlutteringMomentarySatietyPower : LibraryOfRuinaPowerModel
{
    private const int DevourHpThreshold = 20; // 片刻的饕足：可被吞噬的友方精灵畸块体力上限（含）。
    private const int HealPercent = 10; // 片刻的饕足：吞噬后恢复翅振最大体力的百分比。
    private const int VigorAmount = 4; // 片刻的饕足：吞噬后获得的活力层数。

    protected override string LegacyPowerId => "FLUTTERING_MOMENTARY_SATIETY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HealPercent", HealPercent),
        new PowerVar<VigorPower>(VigorAmount),
        new DynamicVar("Threshold", DevourHpThreshold)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VigorPower>()
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (!TurnParticipants.IsRoundPlayerTurn(side) || Owner.IsDead || Owner.Monster is not HistoryFloorFlutteringBoss boss)
        {
            return;
        }

        Creature? food = FlutteringCombatHelper.GetLivingFlutteringMasses(Owner)
            .Where(creature => creature.CurrentHp <= DevourHpThreshold)
            .OrderBy(static creature => creature.CurrentHp)
            .ThenBy(static creature => creature.CombatId ?? 0u)
            .FirstOrDefault();
        if (food == null)
        {
            return;
        }

        Flash();
        await boss.Devour(food);
        await CreatureCmd.Heal(Owner, FlutteringCombatHelper.PercentOfMaxHp(Owner, HealPercent));
        await PowerCmdCompat.Apply<VigorPower>(Owner, VigorAmount, Owner, null);
    }
}

public sealed class FlutteringHungerPower : LibraryOfRuinaPowerModel
{
    internal const int Threshold = 40; // 饥饿：每累计承受该数值伤害，下一回合开始时吞噬一次友方；也是初始计数。
    private const int HealPercent = 20; // 饥饿：吞噬后恢复翅振最大体力的百分比。
    private const int StrengthAmount = 3; // 饥饿：吞噬后永久获得的力量层数。
    private const int MinimumQueuedStacks = 0; // 饥饿：计数降至该值时排入下一回合的吞噬。

    private sealed class Data
    {
        public bool PendingDevour;
    }

    protected override string LegacyPowerId => "FLUTTERING_HUNGER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData()
    {
        return new Data();
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HealPercent", HealPercent),
        new PowerVar<StrengthPower>(StrengthAmount),
        new DynamicVar("Threshold", Threshold),
        new DynamicVar("MinimumStacks", MinimumQueuedStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || Owner.IsDead || result.TotalDamage <= 0)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        if (data.PendingDevour)
        {
            SetAmount(0);
            return;
        }

        int nextAmount = Math.Max(0, Amount - Math.Max(0, result.TotalDamage));
        if (nextAmount == Amount)
        {
            return;
        }

        Flash();
        SetAmount(nextAmount);
        if (nextAmount <= MinimumQueuedStacks)
        {
            data.PendingDevour = true;
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (!TurnParticipants.IsRoundPlayerTurn(side) || Owner.IsDead || Owner.Monster is not HistoryFloorFlutteringBoss boss)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        if (!data.PendingDevour)
        {
            return;
        }

        data.PendingDevour = false;
        Creature? food = FlutteringCombatHelper.GetLivingFlutteringMasses(Owner)
            .OrderBy(static creature => creature.CurrentHp)
            .ThenBy(static creature => creature.CombatId ?? 0u)
            .FirstOrDefault();

        if (food != null)
        {
            Flash();
            await boss.Devour(food);
            await CreatureCmd.Heal(Owner, FlutteringCombatHelper.PercentOfMaxHp(Owner, HealPercent));
            await PowerCmdCompat.Apply<StrengthPower>(Owner, StrengthAmount, Owner, null);
        }

        SetAmount(Threshold);
    }
}

public sealed class FlutteringHungerFrenzyPower : LibraryOfRuinaPowerModel
{
    private const int HpThresholdPercent = 25; // 饥饿狂暴：回合开始时触发所需的体力百分比上限（含），仅触发一次。

    private sealed class Data
    {
        public bool Triggered;
    }

    protected override string LegacyPowerId => "FLUTTERING_HUNGER_FRENZY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData()
    {
        return new Data();
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", HpThresholdPercent)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (!TurnParticipants.IsRoundPlayerTurn(side) || Owner.IsDead || Owner.Monster is not HistoryFloorFlutteringBoss boss)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        if (data.Triggered || Owner.MaxHp <= 0 || Owner.CurrentHp * 100m > Owner.MaxHp * HpThresholdPercent)
        {
            return;
        }

        data.Triggered = true;
        Flash();
        LocalOggOneShotPlayer.Play(HistoryFloorFlutteringBoss.ChangeSfxPath, -1.5f);
        if (Owner is LibraryCreature lc && lc.MaxChaoValue > 0)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(lc, HistoryFloorFlutteringBoss.StaggerResistance);
        }
        await boss.QueueHungerFrenzyAfterStun();
    }
}

public sealed class FlutteringFreshMeatPassivePower : LibraryOfRuinaPowerModel
{
    private const int FirstTriggerTurn = 1;
    private const int TriggerInterval = 2;
    private const int TargetsMarked = 1;

    private sealed class Data
    {
        public int EnemyTurnIndex;
    }

    protected override string LegacyPowerId => "FLUTTERING_FRESH_MEAT_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("FirstTurn", FirstTriggerTurn),
        new DynamicVar("Interval", TriggerInterval),
        new DynamicVar("Targets", TargetsMarked)
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Enemy || Owner.IsDead || Owner.Monster is not HistoryFloorFlutteringBoss boss)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        data.EnemyTurnIndex++;
        if (data.EnemyTurnIndex % TriggerInterval == 0)
        {
            return;
        }

        List<Creature> targets = combatState.LivingPlayerCreatures()
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        Creature? target = boss.RunRng.MonsterAi.NextItem(targets);
        if (target == null)
        {
            return;
        }

        Flash();
        LocalOggOneShotPlayer.Play(HistoryFloorFlutteringBoss.SpecialSfxPath, -1.5f);
        await PowerCmdCompat.Apply<FlutteringFreshMeatPower>(target, 1m, Owner, null);
    }
}

public sealed class FlutteringFreshMeatPower : LibraryOfRuinaPowerModel
{
    private const int DamageTakenIncreasePercent = 25; // 鲜肉：受到翅振攻击时承受伤害提高的百分比。
    private const int LifestealPercent = 100; // 鲜肉：翅振对鲜肉目标造成未被格挡攻击伤害时，按该百分比恢复体力。

    protected override string LegacyPowerId => "FLUTTERING_FRESH_MEAT_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DamageIncrease", DamageTakenIncreasePercent)
    ];

#if STS2_0_111_0
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        if (!IsFlutteringAttackOnOwner(target, props, dealer))
        {
            return 1m;
        }

        return 1m + DamageTakenIncreasePercent / 100m;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!IsFlutteringAttackOnOwner(target, props, dealer)
            || dealer == null
            || dealer.IsDead
            || result.UnblockedDamage <= 0)
        {
            return;
        }

        int healAmount = result.UnblockedDamage * LifestealPercent / 100;
        if (healAmount <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(dealer, healAmount);
    }

    private bool IsFlutteringAttackOnOwner(Creature? target, ValueProp props, Creature? dealer) =>
        target == Owner
        && dealer is { Monster: HistoryFloorFlutteringBoss }
        && ValuePropCompat.IsPoweredAttack(props);
}

public sealed class FlutteringMassCarePower : LibraryOfRuinaPowerModel
{
    private const int HealAmount = 4;

    protected override string LegacyPowerId => "FLUTTERING_MASS_CARE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ScaledMonsterHealVar(HealAmount)
    ];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Enemy || Owner.IsDead)
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(Owner, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Owner, HealAmount));
    }
}

internal static class FlutteringCombatHelper
{
    public static IEnumerable<Creature> GetLivingFlutteringMasses(Creature boss)
    {
        if (boss.CombatState == null)
        {
            return [];
        }

        return boss.CombatState.Enemies.Where(static creature =>
            creature.IsAlive && creature.Monster is HistoryFloorFlutteringMass);
    }

    public static int PercentOfMaxHp(Creature creature, int percent) =>
        Math.Max(1, (int)Math.Ceiling(creature.MaxHp * percent / 100m));
}
