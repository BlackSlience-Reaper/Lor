using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.liberation.History;

[CardPool(typeof(TokenCardPool))]
public sealed class SnowWhiteMaliceChoiceCard : SnowWhiteAppleChoiceCardBase
{
    public override SnowWhiteApplePageMode PageMode => SnowWhiteApplePageMode.Malice;

    protected override string PortraitFileName =>
        "snow_white_malice_choice_card.png";
}
