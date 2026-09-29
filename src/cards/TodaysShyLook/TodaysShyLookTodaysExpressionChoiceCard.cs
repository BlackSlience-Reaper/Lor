using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.TodaysShyLook;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.TodaysShyLook;

[CardPool(typeof(TokenCardPool))]
public sealed class TodaysShyLookTodaysExpressionChoiceCard : TodaysShyLookPageChoiceCardBase
{
    public override TodaysShyLookPageMode PageMode => TodaysShyLookPageMode.TodaysExpression;

    protected override string PortraitFileName => "todays_shy_look_todays_expression_choice_card.png";
}
