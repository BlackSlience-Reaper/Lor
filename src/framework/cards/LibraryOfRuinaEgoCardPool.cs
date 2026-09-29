using Godot;
using LibraryOfRuina.content.liberation.Art;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.content.specialguests.Kali;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.framework.cards;

public sealed class LibraryOfRuinaEgoCardPool : CardPoolModel
{
    public override string Title => "ego";

    public override string EnergyColorName => "ego";

    public override string CardFrameMaterialPath => "card_frame_ego";

    public override Color DeckEntryCardColor => new("D8D0C4FF");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
    [
        ModelDb.Card<EndLightEgoCard>(),
        ModelDb.Card<ForgottenLongingEmbraceEgoCard>(),
        ModelDb.Card<FlutteringHungerFrenzyEgoCard>(),
        ModelDb.Card<PunishmentStrikeEgoCard>(),
        ModelDb.Card<ShatteredLifeEgoCard>(),
        ModelDb.Card<BeyondFragmentEgoCard>(),
        ModelDb.Card<RegretEgoCard>(),
        ModelDb.Card<LimiterReleaseEgoCard>(),
        ModelDb.Card<ChordEgoCard>(),
        ModelDb.Card<SolemnMourningEgoCard>(),
        ModelDb.Card<MagicBulletBaseEgoCard>(),
        ModelDb.Card<MagicBulletPierceEgoCard>(),
        ModelDb.Card<MagicBulletPrecisionEgoCard>(),
        ModelDb.Card<MagicBulletSoulStealEgoCard>(),
        ModelDb.Card<MagicBulletCrueltyEgoCard>(),
        ModelDb.Card<MagicBulletSilenceEgoCard>(),
        ModelDb.Card<MagicBulletTorrentEgoCard>(),
        ModelDb.Card<MagicBulletDespairEgoCard>(),
        ModelDb.Card<RedMistVerticalSplitEgoCard>(),
        ModelDb.Card<RedMistThrustEgoCard>(),
        ModelDb.Card<RedMistHorizontalSlashEgoCard>(),
        ModelDb.Card<RedMistBloodMistEgoCard>(),
        ModelDb.Card<RedMistBattleWillEgoCard>(),
        ModelDb.Card<RedMistFocusBreathEgoCard>(),
        ModelDb.Card<RedMistFieldOfCorpsesEgoCard>()
    ];
}
