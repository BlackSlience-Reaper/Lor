using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

[CardPool(typeof(TokenCardPool))]
public sealed class WrathServantWrathChoiceCard : WrathServantPageChoiceCardBase
{
    public override WrathServantPageMode PageMode => WrathServantPageMode.Wrath;

    protected override string PortraitFileName => "wrath_servant_wrath_choice_card.png";
}
