using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;

namespace LibraryOfRuina.content.abnormalities.CosmicFragment;

public sealed class CosmicFragmentWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "CosmicFragmentBGM",
        GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<CosmicFragment>() };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<CosmicFragment>().ToMutable(), null)
        };
    }
}
