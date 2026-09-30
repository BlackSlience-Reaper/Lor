using System;
using System.Linq;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.assets;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using KaliMonster = LibraryOfRuina.content.specialguests.Kali.Kali;

namespace LibraryOfRuina.content.specialguests.Kali;

public sealed class KaliSpecialGuestEncounter :
    EncounterModel,
    IEncounterBgmSource,
    ISpecialGuestEncounterStage
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.DeathBased(
        "RedMistSpecialGuestBGM",
        SharedAssets.LanguageReceptionFloorGeburabattle1Bgm);

    private const string KaliSlot = "kali";

    public string SpecialGuestId => KaliSpecialGuestIds.Guest;

    public int SpecialGuestStageIndex => 0;

    public override RoomType RoomType => RoomType.Elite;

    public override bool ShouldGiveRewards => true;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => [KaliSlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<KaliMonster>()];

    public override IEnumerable<string> ExtraAssetPaths =>
        KaliMonster.StaticAssetPaths
            .Concat(GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks)
            .Concat(
            [
                GuestReceptionPoolRegistry.LanguageReceptionFloorLayerScenePath,
                KaliSpecialGuestIds.EncounterScene,
            ])
            .Distinct(StringComparer.Ordinal);

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<KaliMonster>().ToMutable(), KaliSlot)];
}
