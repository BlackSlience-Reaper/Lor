using System.Linq;
using LibraryOfRuina.specialguests.Kali;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.RedMist;

public sealed class RedMistElite : EncounterModel
{
    public const string KaliSlot = "kali";
    public const string EncounterScenePath = "res://scenes/encounters/red_mist_elite.tscn";

    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => [KaliSlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Kali>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        Kali.StaticAssetPaths
            .Concat(GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks)
            .Concat(new[]
            {
                GuestReceptionPoolRegistry.LanguageReceptionFloorLayerScenePath,
                EncounterScenePath
            })
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<Kali>().ToMutable(), KaliSlot)
        ];
    }
}
