using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.enchantments.GalaxyChild;

public sealed class PebbleMarkEnchantment : EnchantmentModel
{
    private const int HealAmount = 3;

    public override bool HasExtraCardText => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(HealAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType is CardType.Attack or CardType.Skill or CardType.Power;
    }

    protected override void OnEnchant()
    {
        CardCmd.ApplyKeyword(Card, CardKeyword.Exhaust);
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (!HasCard || Card.Owner?.Creature is not { IsAlive: true } ownerCreature)
        {
            return;
        }

        await CreatureCmd.Heal(ownerCreature, DynamicVars.Heal.BaseValue);
    }
}
