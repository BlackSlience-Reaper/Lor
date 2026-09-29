using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.AllAroundHelper;

[CardPool(typeof(TokenCardPool))]
public sealed class AllAroundHelperRecognitionFunctionChoiceCard : AllAroundHelperPageChoiceCardBase
{
    public override AllAroundHelperPageMode PageMode => AllAroundHelperPageMode.RecognitionFunction;

    protected override string PortraitFileName => "all_around_helper_recognition_function_choice_card.png";
}
