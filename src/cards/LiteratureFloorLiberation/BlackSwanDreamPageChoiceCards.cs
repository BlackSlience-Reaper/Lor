using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.LiteratureFloorLiberation;

public abstract class BlackSwanDreamPageChoiceCardBase : CardModel
{
    public const string FilthChoiceId =
        "BLACK_SWAN_FILTH_CHOICE_CARD";
    public const string BrokenUmbrellaChoiceId =
        "BLACK_SWAN_BROKEN_UMBRELLA_CHOICE_CARD";
    public const string DearFamilyChoiceId =
        "BLACK_SWAN_DEAR_FAMILY_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<SlipperyPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "DebuffMultiplier",
            BlackSwanDreamPageRelic.FilthDebuffMultiplier),
        new DynamicVar(
            "TurnInterval",
            BlackSwanDreamPageRelic.BrokenUmbrellaTurnInterval),
        new DynamicVar(
            "DamageReductionPercent",
            BlackSwanDreamPageRelic.BrokenUmbrellaDamageReductionPercent),
        new DynamicVar(
            "BlockMultiplier",
            BlackSwanDreamPageRelic.BrokenUmbrellaBlockMultiplier),
        new DynamicVar(
            "DearFamilyTurnInterval",
            BlackSwanDreamPageRelic.DearFamilyTurnInterval),
        new PowerVar<SlipperyPower>(
            "Slippery",
            BlackSwanDreamPageRelic.DearFamilySlippery)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            $"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected BlackSwanDreamPageChoiceCardBase()
        : base(
            -1,
            CardType.Skill,
            CardRarity.Ancient,
            TargetType.None,
            shouldShowInCardLibrary: false)
    {
    }

    public static bool IsBlackSwanDreamPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is FilthChoiceId
            or BrokenUmbrellaChoiceId
            or DearFamilyChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class BlackSwanFilthChoiceCard :
    BlackSwanDreamPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "black_swan_filth_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BlackSwanBrokenUmbrellaChoiceCard :
    BlackSwanDreamPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "black_swan_broken_umbrella_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BlackSwanDearFamilyChoiceCard :
    BlackSwanDreamPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "black_swan_dear_family_choice_card.png";
}
