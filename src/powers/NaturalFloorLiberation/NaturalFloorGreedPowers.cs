using System;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.NaturalFloorLiberation;

public abstract class NaturalFloorGreedPassivePower : LibraryOfRuinaPowerModel
{
    protected abstract string IconName { get; }

    public override string PackedIconPath => "res://images/powers/library_of_ruina_" + IconName + "_power.png";

    public override string ResolvedBigIconPath => PackedIconPath;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool AllowNegative => false;

}

public sealed class NaturalFloorFlickeringDesirePower : NaturalFloorGreedPassivePower, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_FLICKERING_DESIRE_POWER";

    protected override string IconName => "flickering_desire";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", NaturalFloorGoldRushBoss.TransformThresholdPercent)
    ];

    private NaturalFloorGoldRushBoss? ProtectedBoss => Owner.Monster is NaturalFloorGoldRushBoss { HasFormHpFloor: true } boss ? boss : null;

    internal bool IsHealthBarLockActive =>
        ProtectedBoss is { } boss && Owner.CurrentHp <= boss.MinimumFormHp;

    public decimal ClampFinalHpLoss(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) =>
        target == Owner && amount > 0 && ProtectedBoss is { } boss
            ? Math.Min(amount, Math.Max(0, Owner.CurrentHp - boss.MinimumFormHp))
            : amount;

    public override bool ShouldDieLate(Creature creature) => creature != Owner || ProtectedBoss == null;

    public override Task AfterPreventingDeath(Creature creature) =>
        creature == Owner ? EnforceFloorAndQueue() : Task.CompletedTask;

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta) =>
        creature == Owner ? EnforceFloorAndQueue() : Task.CompletedTask;

    private async Task EnforceFloorAndQueue()
    {
        if (ProtectedBoss is not { } boss)
        {
            return;
        }

        if (Owner.CurrentHp < boss.MinimumFormHp)
        {
            await CreatureCmd.SetCurrentHp(Owner, boss.MinimumFormHp);
        }

        boss.QueueTransformation();
    }
}

public sealed class NaturalFloorSelfIntoxicationPower : NaturalFloorGreedPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_SELF_INTOXICATION_POWER";

    protected override string IconName => "self_intoxication";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new DynamicVar("Strong", NaturalFloorGoldRushBoss.SelfIntoxicationStrong)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<LibraryStrongPower>()];
}

public sealed class NaturalFloorMomentaryHappinessPower : NaturalFloorGreedPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_MOMENTARY_HAPPINESS_POWER";

    protected override string IconName => "momentary_happiness";
}

public sealed class NaturalFloorKingMomentaryHappinessPower : NaturalFloorGreedPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_KING_MOMENTARY_HAPPINESS_POWER";

    protected override string IconName => "momentary_happiness";
}

public sealed class NaturalFloorKingOfGreedPower : NaturalFloorGreedPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_KING_OF_GREED_POWER";

    protected override string IconName => "king_of_greed_passive";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Bleed", NaturalFloorGoldRushBoss.KingBleeding)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<LibraryBleedingPower>()];

    public override Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || !target.IsPlayer || target.IsDead || !ValuePropCompat.IsPoweredAttack(props))
        {
            return Task.CompletedTask;
        }

        return PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(target, NaturalFloorGoldRushBoss.KingBleeding, Owner, null);
    }
}

public sealed class NaturalFloorGluttonyPower : NaturalFloorGreedPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_GLUTTONY_POWER";

    protected override string IconName => "gluttony";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new DynamicVar("HealPercent", NaturalFloorGoldRushBoss.GluttonyHealPercent)];
}

public sealed class NaturalFloorShiningHappinessPower : NaturalFloorGreedPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_SHINING_HAPPINESS_POWER";

    protected override string IconName => "shining_happiness";

    [SavedProperty]
    public bool AuraApplied { get; private set; }

    [SavedProperty]
    public bool DeathRewardGranted { get; private set; }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", NaturalFloorShiningHappiness.StrongAura),
        new DynamicVar("Endurance", NaturalFloorShiningHappiness.EnduranceAura),
        new DynamicVar("Cards", NaturalFloorShiningHappiness.DeathCards)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>(),
        HoverTipFactory.FromCard<NaturalFloorHappinessShard>()
    ];

    private Creature? Boss => (Owner.CombatState?.Encounter as encounters.NaturalFloorLiberation.NaturalFloorLiberationEncounter)?.GoldRush;

    internal async Task ApplyAura()
    {
        if (AuraApplied || !Owner.IsAlive || Boss is not { IsAlive: true } boss)
        {
            return;
        }

        await KingOfGreed.ShiningHappinessAura.Add<LibraryStrongPower>(boss, NaturalFloorShiningHappiness.StrongAura, Owner);
        await KingOfGreed.ShiningHappinessAura.Add<LibraryEndurancePower>(boss, NaturalFloorShiningHappiness.EnduranceAura, Owner);
        AuraApplied = true;
    }

    internal async Task RemoveAura()
    {
        if (!AuraApplied)
        {
            return;
        }

        AuraApplied = false;
        if (Boss is { } boss)
        {
            await KingOfGreed.ShiningHappinessAura.Remove<LibraryStrongPower>(boss, NaturalFloorShiningHappiness.StrongAura, Owner);
            await KingOfGreed.ShiningHappinessAura.Remove<LibraryEndurancePower>(boss, NaturalFloorShiningHappiness.EnduranceAura, Owner);
        }
    }

    public override Task AfterRemoved(Creature oldOwner) => RemoveAura();

    public override async Task AfterDeath(PlayerChoiceContext context, Creature creature, bool prevented, float animLength)
    {
        if (creature != Owner || prevented)
        {
            return;
        }

        await RemoveAura();
        if (DeathRewardGranted || Owner.Monster is NaturalFloorShiningHappiness { SuppressDeathReward: true })
        {
            return;
        }

        DeathRewardGranted = true;
        await CardPileCmdCompat.AddToCombatAndPreview<NaturalFloorHappinessShard>(
            (Owner.CombatState?.Encounter as encounters.NaturalFloorLiberation.NaturalFloorLiberationEncounter)?.LivingPlayers() ?? [],
            PileType.Hand, NaturalFloorShiningHappiness.DeathCards, addedByPlayer: false);
    }
}
