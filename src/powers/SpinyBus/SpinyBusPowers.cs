using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.SpinyBus;

public sealed class SpinyBusSoftBodyPower : LibraryOfRuinaPowerModel
{
    private const int DamageReductionPercent = 40;
    private const decimal DamageMultiplier = (100 - DamageReductionPercent) / 100m;

    protected override string LegacyPowerId => "SPINY_BUS_SOFT_BODY_POWER";

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

public sealed class SpinyBusUnbearablePleasurePower : LibraryOfRuinaPowerModel
{
    private const int EnergyGain = 1;
    private bool _triggeredThisPlayerTurn;

    protected override string LegacyPowerId => "SPINY_BUS_UNBEARABLE_PLEASURE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(EnergyGain)
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
            || result.UnblockedDamage <= 0
            || dealer is not { IsPlayer: true }
            || attackingPlayer == null
            || Owner.CombatState?.CurrentSide != CombatSide.Player
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        _triggeredThisPlayerTurn = true;
        Flash();
        await PlayerCmd.GainEnergy(EnergyGain, attackingPlayer);
    }
}
