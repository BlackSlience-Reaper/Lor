using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.PunishingBird;

public sealed class PunishingBirdNoBadPower : PunishingBirdBasePower
{
    public const int StrengthPerHit = 1;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override string? LegacyPowerId => "PUNISHING_BIRD_NO_BAD_POWER";

    protected override string IconFileName => "punishing_bird_no_bad_power.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StrengthPerHit", StrengthPerHit)
    ];

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Owner.IsDead
            || target != Owner
            || target.Monster is not monsters.PunishingBird.PunishingBird
            || !ValuePropCompat.IsPoweredAttack(props)
            || result.UnblockedDamage <= 0
            || CombatState.CurrentSide != CombatSide.Player
            || dealer?.IsPlayer != true)
        {
            return;
        }

        await PowerCmdCompat.Apply<StrengthPower>(Owner, StrengthPerHit, Owner, cardSource);
    }
}
