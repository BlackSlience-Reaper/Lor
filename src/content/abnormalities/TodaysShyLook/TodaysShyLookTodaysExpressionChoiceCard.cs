using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.TodaysShyLook;

[CardPool(typeof(TokenCardPool))]
public sealed class TodaysShyLookTodaysExpressionChoiceCard : TodaysShyLookPageChoiceCardBase
{
    public override TodaysShyLookPageMode PageMode => TodaysShyLookPageMode.TodaysExpression;

    protected override string PortraitFileName => "todays_shy_look_todays_expression_choice_card.png";
}
