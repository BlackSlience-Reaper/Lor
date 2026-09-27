using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.HeartOfAspiration;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.powers.HeartOfAspiration;

public sealed class HeartOfAspirationDesirePassivePower : PowerModel
{
    public const int PlatingAmount = 9;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Plating", PlatingAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<PlatingPower>()
    ];

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.IsDead)
        {
            return;
        }

        if (Owner.Monster is not AspirationMonsterBase monster)
        {
            return;
        }

        if (monster.HasDealtLifeDamageThisEnemyTurn)
        {
            monster.ResetLifeDamageFlag();
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<PlatingPower>(
            choiceContext,
            Owner.CombatState!.Enemies,
            PlatingAmount,
            Owner,
            null);
        monster.ResetLifeDamageFlag();
    }
}

public sealed class LungOfAspirationDesirePassivePower : PowerModel
{
    public const int StrengthAmount = 3;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StrengthAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.IsDead)
        {
            return;
        }

        if (Owner.Monster is not AspirationMonsterBase monster)
        {
            return;
        }

        if (monster.HasDealtLifeDamageThisEnemyTurn)
        {
            monster.ResetLifeDamageFlag();
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<StrengthPower>(
            choiceContext,
            Owner.CombatState!.Enemies,
            StrengthAmount,
            Owner,
            null);
        monster.ResetLifeDamageFlag();
    }
}
