using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.enchantments.Yanami;

public sealed class YanamiEtherealEnchantment : EnchantmentModel
{
    public override bool HasExtraCardText => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    public override bool CanEnchant(CardModel card)
    {
        if (!base.CanEnchant(card))
        {
            return false;
        }

        return !card.Keywords.Contains(CardKeyword.Ethereal)
            || !card.Keywords.Contains(CardKeyword.Exhaust);
    }

    protected override void OnEnchant()
    {
        Card.AddKeyword(CardKeyword.Ethereal);
        Card.AddKeyword(CardKeyword.Exhaust);
    }
}
