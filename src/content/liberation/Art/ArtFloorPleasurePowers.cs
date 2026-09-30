using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Art;

public sealed class ArtFloorPleasureJoyThornsPower : LibraryOfRuinaPowerModel
{
    public const int PlayerThorns = 1;
    public const int SelfThorns = 2;

    private Dictionary<Creature, int> _playerThornsGranted = [];
    private bool _cleanedUpPlayerThorns;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _playerThornsGranted = new(_playerThornsGranted);
        _cleanedUpPlayerThorns = false;
    }

    protected override string LegacyPowerId => "ART_FLOOR_PLEASURE_JOY_THORNS_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ThornsPower>("PlayerThorns", PlayerThorns),
        new PowerVar<ThornsPower>("SelfThorns", SelfThorns)
    ];

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.IsDead || Owner.CombatState == null)
        {
            return;
        }

        IReadOnlyList<Creature> players = Owner.CombatState.LivingPlayerCreatures()
            .ToArray();

        Flash();
        if (players.Count > 0)
        {
            await PowerCmdCompat.Apply<ThornsPower>(
                choiceContext,
                players,
                PlayerThorns,
                Owner,
                null);

            foreach (Creature player in players)
            {
                _playerThornsGranted[player] = _playerThornsGranted.TryGetValue(player, out int granted)
                    ? granted + PlayerThorns
                    : PlayerThorns;
            }
        }

        await PowerCmdCompat.Apply<ThornsPower>(
            choiceContext,
            Owner,
            SelfThorns,
            Owner,
            null);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        return creature == Owner
            ? CleanupPlayerThorns(choiceContext)
            : Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        return CleanupPlayerThorns(new ThrowingPlayerChoiceContext());
    }

    private async Task CleanupPlayerThorns(PlayerChoiceContext choiceContext)
    {
        if (_cleanedUpPlayerThorns || _playerThornsGranted.Count == 0)
        {
            return;
        }

        _cleanedUpPlayerThorns = true;
        foreach ((Creature player, int granted) in _playerThornsGranted.ToArray())
        {
            if (granted <= 0)
            {
                continue;
            }

            ThornsPower? thorns = player.GetPower<ThornsPower>();
            if (thorns == null || thorns.Amount <= 0)
            {
                continue;
            }

            int amountToRemove = Math.Min(thorns.Amount, granted);
            if (amountToRemove >= thorns.Amount)
            {
                await PowerCmd.Remove(thorns);
            }
            else
            {
                await PowerCmdCompat.ModifyAmount(
                    choiceContext,
                    thorns,
                    -amountToRemove,
                    Owner,
                    null,
                    silent: true);
            }
        }

        _playerThornsGranted.Clear();
    }
}

public sealed class ArtFloorPleasureSoftBodyPower : LibraryOfRuinaPowerModel
{
    public const int DamageReductionPercent = 50;
    private const decimal DamageMultiplier = (100 - DamageReductionPercent) / 100m;

    protected override string LegacyPowerId => "ART_FLOOR_PLEASURE_SOFT_BODY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DamageReduction", DamageReductionPercent)
    ];

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m || ValuePropCompat.IsPoweredAttack(props))
        {
            return amount;
        }

        return amount * DamageMultiplier;
    }

    public override Task AfterModifyingHpLostAfterOsty()
    {
        Flash();
        return Task.CompletedTask;
    }
}

public sealed class ArtFloorPleasureUnbearablePleasurePower : LibraryOfRuinaPowerModel
{
    public const int EnergyLoss = 2;

    private bool _triggeredThisPlayerTurn;

    protected override string LegacyPowerId => "ART_FLOOR_PLEASURE_UNBEARABLE_PLEASURE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(EnergyLoss)
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            _triggeredThisPlayerTurn = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        var attackingPlayer = dealer?.Player;
        if (_triggeredThisPlayerTurn
            || target != Owner
            || result.UnblockedDamage <= 0m
            || dealer is not { IsPlayer: true }
            || attackingPlayer == null
            || Owner.CombatState?.CurrentSide != CombatSide.Player
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        _triggeredThisPlayerTurn = true;
        Flash();
        await PlayerCmd.LoseEnergy(EnergyLoss, attackingPlayer);
    }
}

public sealed class ArtFloorPleasureExplodingHeadPower : LibraryOfRuinaPowerModel
{
    public const int Threshold = 3;

    protected override string LegacyPowerId => "ART_FLOOR_PLEASURE_EXPLODING_HEAD_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", Threshold)
    ];

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        CombatStateLike? combatState = Owner.CombatState;
        if (side != CombatSide.Player
            || Owner.IsDead
            || Owner.Monster is not ArtFloorPleasureBoss boss
            || combatState == null
            || !AnyPlayerHasPleasureThreshold(combatState))
        {
            return Task.CompletedTask;
        }

        Flash();
        return boss.QueuePleasureEgo();
    }

    private static bool AnyPlayerHasPleasureThreshold(CombatStateLike combatState)
    {
        return combatState.Players.Any(static player =>
            player.Creature.IsAlive
            && PileType.Hand.GetPile(player).Cards.Count(static card => card is PleasureCard) >= Threshold);
    }
}
