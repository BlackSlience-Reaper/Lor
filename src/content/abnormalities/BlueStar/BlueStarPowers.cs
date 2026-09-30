using System;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.BlueStar;

public sealed class BlueStarDivinePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BLUE_STAR_DIVINE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldAllowTargeting(Creature target) =>
        target != Owner;

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner
            || amount <= 0m
            || canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            return false;
        }

        modifiedAmount = 0m;
        return true;
    }

    public override Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        Flash();
        return Task.CompletedTask;
    }
}

public sealed class BlueStarNovaVoicePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BLUE_STAR_NOVA_VOICE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", BlueStarAltar.NovaInterval),
        new DynamicVar("Confusion", BlueStarAltar.NovaConfusion),
        new DynamicVar("ChaoHealPercent", BlueStarAltar.NovaFollowerChaoHealPercent)
    ];
}

public sealed class BlueStarReturnToStarsPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "BLUE_STAR_RETURN_TO_STARS_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLossPercent", BlueStarAltar.ReturnHpLossPercent)
    ];

    public override async Task AfterStun(Creature creature)
    {
        if (Owner.IsDead
            || creature.IsDead
            || creature.Monster is not BlueStarFollower
            || !BlueStarEncounterHelper.IsBlueStarEncounter(Owner.CombatState))
        {
            return;
        }

        Flash();
        await CreatureCmd.Kill(creature);

        if (Owner.IsDead)
        {
            return;
        }

        decimal hpLoss = Math.Ceiling(
            Owner.MaxHp * BlueStarAltar.ReturnHpLossPercent / 100m);
        await CreatureCmd.SetCurrentHp(
            Owner,
            Math.Max(0m, Owner.CurrentHp - hpLoss));
    }
}

public sealed class BlueStarMartyrPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BLUE_STAR_MARTYR_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Followers", BlueStarFollower.RequiredFollowerCount)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player
            || Owner.IsDead
            || !BlueStarEncounterHelper.IsBlueStarEncounter(combatState)
            || BlueStarEncounterHelper.LivingFollowers(combatState).Count
                >= BlueStarFollower.RequiredFollowerCount)
        {
            return;
        }

        string? slot = BlueStarEncounterHelper.FirstMissingFollowerSlot(
            combatState);
        if (slot == null)
        {
            return;
        }

        Flash();
        LocalOggOneShotPlayer.Play(BlueStarAltar.InSfxPath, -2f);
        await CreatureCmd.Add<BlueStarFollower>(combatState, slot);
    }
}

public sealed class BlueStarFollowerVoicePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "BLUE_STAR_FOLLOWER_VOICE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ThresholdPercent", BlueStarFollower.SelfDestructThresholdPercent)
    ];
}

public sealed class BlueStarMartyrdomPower : LibraryOfRuinaPowerModel
{
    private bool _isResolving;

    protected override string LegacyPowerId =>
        "BLUE_STAR_MARTYRDOM_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType =>
        PowerInstanceType.Instanced;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HpLossVar(BlueStarMartyrdomCard.SelfHpLoss)
    ];

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (_isResolving
            || Owner.IsDead
            || dealer != Owner
            || target.Side == Owner.Side
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        _isResolving = true;
        try
        {
            Flash();
            LocalOggOneShotPlayer.Play(BlueStarAltar.SubAttackSfxPath, -5f);
            if (target is LibraryCreature
                {
                    IsDead: false,
                    HasChaoResistance: true,
                    MaxChaoValue: > 0
                })
            {
                await LibraryCreatureCmd.ChaoDamage(
                    choiceContext,
                    [target],
                    Amount,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    Owner,
                    cardSource,
                    null);
            }

            if (!Owner.IsDead)
            {
                await CreatureCmd.Damage(
                    choiceContext,
                    Owner,
                    BlueStarMartyrdomCard.SelfHpLoss,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    Owner);
            }
        }
        finally
        {
            _isResolving = false;
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }
}
