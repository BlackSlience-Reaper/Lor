using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.guests.DawnOffice;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.abnormalities.BigBadWolf;

public sealed class BigBadWolfPunishEvilPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BIG_BAD_WOLF_PUNISH_EVIL_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLossPercent", BigBadWolf.SpitOutHpLossPercent)
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return side == CombatSide.Player && Owner.Monster is BigBadWolf wolf
            ? wolf.BeginSwallowVulnerabilityWindow(choiceContext)
            : Task.CompletedTask;
    }
}

public sealed class BigBadWolfBornToBePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BIG_BAD_WOLF_BORN_TO_BE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<LibraryOfRuinaNextTurnStrength>("NextTurnStrength", BigBadWolf.BornToBeNextTurnStrength)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaNextTurnStrength>()
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return side == CombatSide.Enemy && Owner.Monster is BigBadWolf wolf
            ? wolf.ResolveBornToBe(choiceContext)
            : Task.CompletedTask;
    }
}

public sealed class BigBadWolfTemporaryThornsPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BIG_BAD_WOLF_TEMPORARY_THORNS_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != Owner.Side || Amount <= 0)
        {
            return;
        }

        ThornsPower? thorns = Owner.GetPower<ThornsPower>();
        if (thorns != null)
        {
            await PowerCmdCompat.ModifyAmount(choiceContext, thorns, -Amount, Owner, null, silent: true);
        }

        await PowerCmd.Remove(this);
    }
}
