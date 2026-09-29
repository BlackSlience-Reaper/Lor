using System.Linq;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.monsters.SmilingBodies;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.SmilingBodies;

public sealed class SmilingBodiesStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "SmilingBodiesBGM",
        GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public const string CorpseSlotOne = "corpse_1";
    public const string CorpseSlotTwo = "corpse_2";
    public const string BossSlot = "boss";
    public const string CorpseSlotThree = "corpse_3";
    public const string CorpseSlotFour = "corpse_4";

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots =>
    [
        CorpseSlotOne,
        CorpseSlotTwo,
        BossSlot,
        CorpseSlotThree,
        CorpseSlotFour
    ];

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<monsters.SmilingBodies.SmilingBodies>(),
        ModelDb.Monster<MeltingCorpse>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        monsters.SmilingBodies.SmilingBodies.AssetPathsStatic
            .Concat(MeltingCorpse.AssetPathsStatic)
            .Concat(GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks)
            .Concat(new[]
            {
                "res://images/backgrounds/smiling_bodies_strong/background.png"
            })
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<MeltingCorpse>().ToMutable(), CorpseSlotOne),
            (ModelDb.Monster<monsters.SmilingBodies.SmilingBodies>().ToMutable(), BossSlot)
        ];
    }
}

internal static class SmilingBodiesEncounterHelper
{
    public static bool IsSmilingBodiesEncounter(CombatStateLike? combatState)
    {
        return combatState?.RunState.CurrentRoom is CombatRoom room
            && room.Encounter.MonstersWithSlots.Any(pair =>
                pair.Item1 is monsters.SmilingBodies.SmilingBodies or MeltingCorpse);
    }
}
