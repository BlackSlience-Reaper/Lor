using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.ScaredyCat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.RoadHome;

public abstract class RoadHomePowerModel : LibraryOfRuinaPowerModel
{
    protected abstract string IconFileName { get; }

    public override string PackedIconPath => ImageHelper.GetImagePath("powers/" + IconFileName);

    public override string ResolvedBigIconPath => PackedIconPath;
}

public sealed class RoadHomeBadWizardPassivePower : RoadHomePowerModel
{
    protected override string LegacyPowerId => "ROAD_HOME_BAD_WIZARD_PASSIVE_POWER";

    protected override string IconFileName => "road_home_bad_wizard_passive_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Interrupts", monsters.RoadHome.RoadHome.TotalInterruptsForBadWizard),
        new DynamicVar("Damage", monsters.RoadHome.RoadHome.BadWizardGroupLowDamage),
        new DynamicVar("HighDamage", monsters.RoadHome.RoadHome.BadWizardGroupHighDamage)
    ];
}

public sealed class RoadHomeFriendPassivePower : RoadHomePowerModel
{
    protected override string LegacyPowerId => "ROAD_HOME_FRIEND_PASSIVE_POWER";

    protected override string IconFileName => "road_home_friend_passive_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Interrupts", monsters.RoadHome.RoadHome.FriendFullyInterruptedTurnsForModeThree)
    ];
}

public sealed class ScaredyCatCouragePower : RoadHomePowerModel
{
    protected override string LegacyPowerId => "SCAREDY_CAT_COURAGE_POWER";

    protected override string IconFileName => "scaredy_cat_courage_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", ScaredyCat.CourageStrong),
        new DynamicVar("Turns", ScaredyCat.OneTurnDuration)
    ];
}

public sealed class ScaredyCatCowardPower : RoadHomePowerModel
{
    protected override string LegacyPowerId => "SCAREDY_CAT_COWARD_POWER";

    protected override string IconFileName => "scaredy_cat_coward_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class RoadHomeHouseProtectionPower : RoadHomePowerModel, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    protected override string LegacyPowerId => "ROAD_HOME_HOUSE_PROTECTION_POWER";

    protected override string IconFileName => "road_home_house_protection_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m || !IsPlayerSource(dealer, cardSource))
        {
            return amount;
        }

        return 0m;
    }

    public override Task AfterModifyingHpLostAfterOsty()
    {
        Flash();
        return Task.CompletedTask;
    }

    private static bool IsPlayerSource(Creature? dealer, CardModel? cardSource)
    {
        if (dealer?.IsPlayer == true || dealer?.PetOwner != null)
        {
            return true;
        }

        return cardSource?.Owner?.Creature?.IsPlayer == true;
    }
}

public sealed class ScaredyCatCompanionCowardPower : RoadHomePowerModel
{
    protected override string LegacyPowerId => "SCAREDY_CAT_COMPANION_COWARD_POWER";

    protected override string IconFileName => "scaredy_cat_companion_coward_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool OwnerIsSecondaryEnemy => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLoss", ScaredyCatCompanion.EndTurnHpLoss)
    ];

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.IsDead || side != Owner.Side || !participants.Contains(Owner))
        {
            return;
        }

        await CreatureCmdCompat.Damage(
            choiceContext,
            Owner,
            ScaredyCatCompanion.EndTurnHpLoss,
            ValueProp.Unpowered | ValueProp.Unpowered,
            Owner,
            null);
    }
}
