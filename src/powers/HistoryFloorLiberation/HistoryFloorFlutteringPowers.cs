using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.HistoryFloorLiberation;

public sealed class FlutteringMomentarySatietyPower : LibraryOfRuinaPowerModel
{
    private const int DevourHpThreshold = 20;
    private const int HealAmount = 20;
    private const int VigorAmount = 2;

    protected override string LegacyPowerId => "FLUTTERING_MOMENTARY_SATIETY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ScaledMonsterHealVar(HealAmount),
        new PowerVar<VigorPower>(VigorAmount),
        new DynamicVar("Threshold", DevourHpThreshold)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VigorPower>()
    ];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Enemy || Owner.IsDead || Owner.Monster is not HistoryFloorFlutteringBoss boss)
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
        await CreatureCmd.Heal(Owner, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Owner, HealAmount));
        await PowerCmdCompat.Apply<VigorPower>(Owner, VigorAmount, Owner, null);
    }
}

public sealed class FlutteringHungerPower : LibraryOfRuinaPowerModel
{
    private const int Threshold = 40;
    private const int HealAmount = 40;
    private const int StrengthAmount = 2;
    private const int MinimumQueuedStacks = 0;

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
        new ScaledMonsterHealVar(HealAmount),
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

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Enemy || Owner.IsDead || Owner.Monster is not HistoryFloorFlutteringBoss boss)
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
            await CreatureCmd.Heal(Owner, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Owner, HealAmount));
            await PowerCmdCompat.Apply<StrengthPower>(Owner, StrengthAmount, Owner, null);
        }

        SetAmount(Threshold);
    }
}

public sealed class FlutteringHungerFrenzyPower : LibraryOfRuinaPowerModel
{
    private const int HpThresholdPercent = 25;

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

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature == Owner && delta < 0)
        {
            await TryQueueHungerFrenzy();
        }
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side == CombatSide.Enemy)
        {
            await TryQueueHungerFrenzy();
        }
    }

    private async Task TryQueueHungerFrenzy()
    {
        if (Owner.IsDead || Owner.Monster is not HistoryFloorFlutteringBoss boss)
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

        List<Creature> targets = combatState.PlayerCreatures
            .Where(static creature => creature.IsAlive)
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
    private const int HealPerHit = 10;
    private const int NextTurnStrength = 1;

    private sealed class Data
    {
        public AttackCommand? CurrentAttack;
        public Creature? CurrentBoss;
        public int CurrentAttackGroupId;
        public int LastStrengthAttackGroupId;
    }

    protected override string LegacyPowerId => "FLUTTERING_FRESH_MEAT_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData()
    {
        return new Data();
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ScaledMonsterHealVar(HealPerHit),
        new PowerVar<LibraryOfRuinaNextTurnStrength>(NextTurnStrength)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaNextTurnStrength>()
    ];

    public override Task BeforeAttack(AttackCommand command)
    {
        Data data = GetInternalData<Data>();
        data.CurrentAttack = null;
        data.CurrentBoss = null;

        if (command.Attacker is { Monster: HistoryFloorFlutteringBoss boss } attacker)
        {
            data.CurrentAttack = command;
            data.CurrentBoss = attacker;
            data.CurrentAttackGroupId = boss.CurrentFreshMeatAttackGroupId > 0
                ? boss.CurrentFreshMeatAttackGroupId
                : RuntimeHelpers.GetHashCode(command);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        Data data = GetInternalData<Data>();
        if (data.CurrentAttack != command || data.CurrentBoss == null || data.CurrentBoss.IsDead)
        {
            return;
        }

        try
        {
            DamageResult[] ownerResults = AttackCommandCompat.Results(command)
                .Where(result => result.Receiver == Owner)
                .ToArray();
            if (ownerResults.Length == 0)
            {
                return;
            }

            int unblockedHits = ownerResults.Count(static result => result.UnblockedDamage > 0);
            Flash();
            if (data.LastStrengthAttackGroupId != data.CurrentAttackGroupId)
            {
                data.LastStrengthAttackGroupId = data.CurrentAttackGroupId;
                await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(
                    data.CurrentBoss,
                    NextTurnStrength,
                    data.CurrentBoss,
                    null);
            }

            for (int i = 0; i < unblockedHits; i++)
            {
                await CreatureCmd.Heal(data.CurrentBoss, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(data.CurrentBoss, HealPerHit));
            }
        }
        finally
        {
            data.CurrentAttack = null;
            data.CurrentBoss = null;
            data.CurrentAttackGroupId = 0;
        }
    }
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
}
