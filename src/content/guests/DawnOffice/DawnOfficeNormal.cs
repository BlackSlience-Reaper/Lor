using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.guests.DawnOffice;

public sealed class DawnOfficeNormal : EncounterModel, IGuestReceptionEncounter, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.DeathBased(
        "DawnOfficeBGM",
        DawnOfficeAssets.Battle1Bgm,
        DawnOfficeAssets.Battle2Bgm,
        DawnOfficeAssets.Battle3Bgm);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "salvador", "yuna", "philip" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Salvador>(),
            ModelDb.Monster<Yuna>(),
            ModelDb.Monster<Philip>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<Salvador>().ToMutable(), "salvador"),
            (ModelDb.Monster<Yuna>().ToMutable(), "yuna"),
            (ModelDb.Monster<Philip>().ToMutable(), "philip")
        };
    }
}
