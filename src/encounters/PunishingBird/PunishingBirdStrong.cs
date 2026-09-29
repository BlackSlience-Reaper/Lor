using System.Linq;
using LibraryOfRuina.monsters.PunishingBird;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.PunishingBird;

public sealed class PunishingBirdStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "PunishingBirdBGM",
        GuestReceptionPoolRegistry.PhilosophyReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public const string LeftKeeperSlot = "punishing_bird_left_keeper";
    public const string BossSlot = "punishing_bird_boss";
    public const string RightKeeperSlot = "punishing_bird_right_keeper";

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => false;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => [LeftKeeperSlot, BossSlot, RightKeeperSlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<ForestKeeperBirdLeft>(),
        ModelDb.Monster<monsters.PunishingBird.PunishingBird>(),
        ModelDb.Monster<ForestKeeperBirdRight>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<ForestKeeperBirdLeft>().ToMutable(), LeftKeeperSlot),
        (ModelDb.Monster<monsters.PunishingBird.PunishingBird>().ToMutable(), BossSlot),
        (ModelDb.Monster<ForestKeeperBirdRight>().ToMutable(), RightKeeperSlot)
    ];

    public override float GetCameraScaling() => 0.82f;
}

internal static class PunishingBirdEncounterHelper
{
    public static bool IsEncounter(CombatStateLike? combatState) =>
        combatState?.RunState.CurrentRoom is CombatRoom room
        && room.Encounter.MonstersWithSlots.Any(static pair =>
            pair.Item1 is monsters.PunishingBird.PunishingBird);

    public static IReadOnlyList<Creature> LivingPlayers(CombatStateLike? combatState) =>
        combatState?.Players
            .Select(static player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .ToArray()
        ?? [];

    public static IReadOnlyList<Creature> LivingKeepers(CombatStateLike? combatState) =>
        combatState?.Enemies
            .Where(static creature => creature.IsAlive && creature.Monster is ForestKeeperBirdBase)
            .OrderBy(static creature => creature.Monster is ForestKeeperBirdLeft ? 0 : 1)
            .ThenBy(static creature => creature.CombatId ?? uint.MaxValue)
            .ToArray()
        ?? [];

    public static Creature? FindPunishingBird(CombatStateLike? combatState) =>
        combatState?.Enemies.FirstOrDefault(static creature =>
            creature.IsAlive && creature.Monster is monsters.PunishingBird.PunishingBird);
}
