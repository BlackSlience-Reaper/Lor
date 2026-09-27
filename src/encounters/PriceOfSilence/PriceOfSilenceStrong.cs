using System.Linq;
using LibraryOfRuina.monsters.PriceOfSilence;
using LibraryOfRuina.powers.PriceOfSilence;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.PriceOfSilence;

public sealed class PriceOfSilenceStrong : EncounterModel
{
    public const string TraceSlot = "time_trace";
    public const string BossSlot = "price_of_silence_boss";

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => false;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots =>
    [
        TraceSlot,
        BossSlot
    ];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<TimeTrace>(),
        ModelDb.Monster<monsters.PriceOfSilence.PriceOfSilence>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<monsters.PriceOfSilence.PriceOfSilence>().ToMutable(), BossSlot),
            (ModelDb.Monster<TimeTrace>().ToMutable(), TraceSlot)
        ];
    }

    public override float GetCameraScaling() => 0.82f;
}

internal static class PriceOfSilenceEncounterHelper
{
    public static bool IsPriceOfSilenceEncounter(CombatStateLike? combatState)
    {
        return combatState?.RunState.CurrentRoom is CombatRoom room
            && room.Encounter.MonstersWithSlots.Any(pair =>
                pair.Item1 is monsters.PriceOfSilence.PriceOfSilence or TimeTrace);
    }

    public static Creature? FindBoss(CombatStateLike? combatState)
    {
        return combatState?.Creatures.FirstOrDefault(creature =>
            creature.IsAlive && creature.Monster is monsters.PriceOfSilence.PriceOfSilence);
    }

    public static PriceOfSilenceEncounterTrackerPower? FindTracker(CombatStateLike? combatState)
    {
        return FindBoss(combatState)?.GetPower<PriceOfSilenceEncounterTrackerPower>();
    }

    public static IReadOnlyList<Creature> LivingPlayers(CombatStateLike? combatState)
    {
        return combatState?.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .ToArray()
            ?? [];
    }
}
