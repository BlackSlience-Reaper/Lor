using Godot;
using LibraryOfRuina.cards.Xiao;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards;

/// <summary>
/// Character-owned combat E.G.O pages.  This is deliberately separate from
/// the abnormality/floor E.G.O pool represented by LibraryOfRuinaEgoCardPool.
/// </summary>
public sealed class LibraryOfRuinaCharacterEgoCardPool : CardPoolModel
{
    public override string Title => "character_ego";

    public override string EnergyColorName => "character_ego";

    public override string CardFrameMaterialPath => "card_frame_red";

    public override Color DeckEntryCardColor => new("DC2828FF");

    public override Color EnergyOutlineColor => new("6E1414FF");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
    [
        ModelDb.Card<XiaoPulaoBellEgoCard>(),
        ModelDb.Card<XiaoYaziVengeanceEgoCard>(),
        ModelDb.Card<XiaoTaotieFeastEgoCard>()
    ];
}
