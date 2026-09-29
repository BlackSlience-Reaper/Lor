using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.liberation.Language;

public abstract class NothingTherePageChoiceCardBase : PageChoiceCard<NothingTherePageMode>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "DamageMultiplier",
            NothingTherePageRelic.GoodbyeDamageMultiplier)
    ];
}

[CardPool(typeof(TokenCardPool))]
public sealed class NothingThereGoodbyeChoiceCard :
    NothingTherePageChoiceCardBase
{
    public override NothingTherePageMode PageMode => NothingTherePageMode.Goodbye;

    protected override string PortraitFileName =>
        "nothing_there_goodbye_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NothingThereHelloChoiceCard :
    NothingTherePageChoiceCardBase
{
    public override NothingTherePageMode PageMode => NothingTherePageMode.Hello;

    protected override string PortraitFileName =>
        "nothing_there_hello_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NothingThereShellChoiceCard :
    NothingTherePageChoiceCardBase
{
    public override NothingTherePageMode PageMode => NothingTherePageMode.Shell;

    protected override string PortraitFileName =>
        "nothing_there_shell_choice_card.png";
}
