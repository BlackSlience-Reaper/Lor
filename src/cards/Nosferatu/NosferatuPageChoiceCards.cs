using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.Nosferatu;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.Nosferatu;

public abstract class NosferatuPageChoiceCardBase : PageChoiceCard<NosferatuPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HydrophobiaBleed", NosferatuPageRelic.HydrophobiaBleed),
        new DynamicVar("DamageBonus", NosferatuPageRelic.VampirismDamageBonus),
        new HealVar("VampirismHeal", NosferatuPageRelic.VampirismHeal),
        new HealVar("WineHeal", NosferatuPageRelic.WineHeal)
    ];
}

[CardPool(typeof(TokenCardPool))]
public sealed class NosferatuHydrophobiaChoiceCard : NosferatuPageChoiceCardBase
{
    public override NosferatuPageMode PageMode => NosferatuPageMode.Hydrophobia;

    protected override string PortraitFileName => "nosferatu_hydrophobia_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NosferatuVampirismChoiceCard : NosferatuPageChoiceCardBase
{
    public override NosferatuPageMode PageMode => NosferatuPageMode.Vampirism;

    protected override string PortraitFileName => "nosferatu_vampirism_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NosferatuWineChoiceCard : NosferatuPageChoiceCardBase
{
    public override NosferatuPageMode PageMode => NosferatuPageMode.Wine;

    protected override string PortraitFileName => "nosferatu_wine_choice_card.png";
}
