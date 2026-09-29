using LibraryOfRuina.guests;
using LibraryOfRuina.guests.DawnOffice;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.DawnOffice;

public sealed class FinnWeak : EncounterModel, IGuestReceptionEncounter, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.DeathBased(
        "FinnBGM",
        "res://audio/bgm/finn/finn_battle_1.ogg");

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "finn" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Finn>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<Finn>().ToMutable(), "finn")
        ];
    }
}
