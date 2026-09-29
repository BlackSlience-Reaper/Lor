using System.Threading.Tasks;
using LibraryLib.Combat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.framework.powers;

/// <summary>
/// One-round Power Nullification. The holder's dice keep their printed move
/// values and resistance, but ignore every Power increase or decrease.
/// </summary>
public sealed class NullifyPower :
    LibraryOfRuinaPowerModel,
    ILibraryCombatValueResolutionPolicy
{
    private bool _skipNextEnemyTurnEndRemoval;

    protected override string LegacyPowerId => "NULLIFY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/nullify_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _ = applier;
        _ = cardSource;
        _skipNextEnemyTurnEndRemoval =
            Owner.CombatState?.CurrentSide == CombatSide.Enemy;
        return Task.CompletedTask;
    }

    public LibraryCombatValueResolution GetCombatValueResolution(
        in LibraryCombatValueContext context)
    {
        if (!AffectsOwnerDice(in context))
        {
            return LibraryCombatValueResolution.Default;
        }

        // 威力无效只还原卡牌与怪物招式的基础数值，其他来源照常结算。
        return ValuePropCompat.IsPoweredAttack(context.Props)
            ? LibraryCombatValueResolution.BaseValueAndResistanceOnly
            : LibraryCombatValueResolution.Default;
    }

    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        _ = choiceContext;
        _ = participants;
        if (side == CombatSide.Enemy)
        {
            if (_skipNextEnemyTurnEndRemoval)
            {
                _skipNextEnemyTurnEndRemoval = false;
                return;
            }

            await PowerCmd.Remove(this);
        }
    }

    private bool AffectsOwnerDice(in LibraryCombatValueContext context) =>
        context.Kind switch
        {
            LibraryCombatValueKind.PhysicalDamage or
                LibraryCombatValueKind.ChaoDamage => context.Dealer == Owner,
            LibraryCombatValueKind.Block => context.Target == Owner,
            _ => false,
        };
}
