using System.Threading.Tasks;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.relics.StandaloneRelics;

public sealed class LoveBentoRelic : YanamiRelicModel
{
    private const int TastyCards = 1;

    protected override string IconBaseName => "love_bento_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(TastyCards)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromCardWithCardHoverTips<TastyCard>();

    public override async Task AfterObtained()
    {
        var tasty = Owner.RunState.CreateCard<TastyCard>(Owner);
        SaveManager.Instance.MarkCardAsSeen(tasty);
        var result = await CardPileCmd.Add(tasty, PileType.Deck);
        CardCmd.PreviewCardPileAdd(result);
        
    }
}
