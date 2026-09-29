using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.AllAroundHelper;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.AllAroundHelper;

[CardPool(typeof(TokenCardPool))]
public sealed class AllAroundHelperRecognitionFunctionChoiceCard : AllAroundHelperPageChoiceCardBase
{
    public override AllAroundHelperPageMode PageMode => AllAroundHelperPageMode.RecognitionFunction;

    protected override string PortraitFileName => "all_around_helper_recognition_function_choice_card.png";
}
