using LibraryOfRuina.content.guests.MusiciansOfBremen;
using LibraryOfRuina.content.guests.WedgeOffice;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.guests.BrotherhoodOfIron;

public sealed class BrotherhoodOfIronStrong : EncounterModel, IGuestReceptionEncounter, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "BrotherhoodOfIronBGM",
        GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "mo", "consta", "arnold" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Mo>(),
            ModelDb.Monster<Consta>(),
            ModelDb.Monster<Arnold>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<Mo>().ToMutable(), "mo"),
            (ModelDb.Monster<Consta>().ToMutable(), "consta"),
            (ModelDb.Monster<Arnold>().ToMutable(), "arnold")
        };
    }
}
