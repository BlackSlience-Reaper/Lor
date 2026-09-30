using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.guests.KuroKumo;

public sealed class KuroKumoNormal : EncounterModel, IGuestReceptionEncounter, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.DeathBased(
        "KuroKumoBGM",
        KuroKumoAssets.KurokumoGuestBattle1Bgm,
        KuroKumoAssets.KurokumoGuestBattle2Bgm,
        KuroKumoAssets.KurokumoGuestBattle3Bgm);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "yang", "sayo", "gin" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Yang>(),
            ModelDb.Monster<Sayo>(),
            ModelDb.Monster<Gin>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<Yang>().ToMutable(), "yang"),
            (ModelDb.Monster<Sayo>().ToMutable(), "sayo"),
            (ModelDb.Monster<Gin>().ToMutable(), "gin")
        };
    }
}
