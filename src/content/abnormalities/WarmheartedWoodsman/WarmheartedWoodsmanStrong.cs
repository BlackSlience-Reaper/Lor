using System.Linq;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;

public sealed class WarmheartedWoodsmanStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "WarmheartedWoodsmanBGM",
        GuestReceptionPoolRegistry.SocialSciencesReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public const string TreeSlot = "woodsman_tree_left";
    public const string WoodsmanSlot = "warmhearted_woodsman_right";

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => false;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => [TreeSlot, WoodsmanSlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<WoodsmanTree>(),
        ModelDb.Monster<WarmheartedWoodsman>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<WoodsmanTree>().ToMutable(), TreeSlot),
            (ModelDb.Monster<WarmheartedWoodsman>().ToMutable(), WoodsmanSlot)
        ];
    }

    public override float GetCameraScaling() => 0.9f;
}

internal static class WarmheartedWoodsmanEncounterHelper
{
    public static bool IsWarmheartedWoodsmanEncounter(CombatStateLike? combatState)
    {
        return combatState?.RunState.CurrentRoom is CombatRoom room
            && room.Encounter.MonstersWithSlots.Any(pair =>
                pair.Item1 is WarmheartedWoodsman or WoodsmanTree);
    }

    public static Creature? FindTree(CombatStateLike? combatState)
    {
        return combatState?.Creatures.FirstOrDefault(creature =>
            creature.IsAlive && creature.Monster is WoodsmanTree);
    }

    public static Creature? FindWoodsman(CombatStateLike? combatState)
    {
        return combatState?.Creatures.FirstOrDefault(creature =>
            creature.IsAlive && creature.Monster is WarmheartedWoodsman);
    }
}
