using System;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.Ozma;

public sealed class OzmaElite : EncounterModel
{
    public const string EastJackSlot = "ozma_jack_east";
    public const string SouthJackSlot = "ozma_jack_south";
    public const string WestJackSlot = "ozma_jack_west";
    public const string NorthJackSlot = "ozma_jack_north";
    public const string OzmaSlot = "ozma";

    public static readonly string[] JackSlots =
    [
        EastJackSlot,
        SouthJackSlot,
        WestJackSlot,
        NorthJackSlot
    ];

    public override RoomType RoomType => RoomType.Elite;

    public override bool IsWeak => false;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots =>
    [
        EastJackSlot,
        SouthJackSlot,
        WestJackSlot,
        NorthJackSlot,
        OzmaSlot
    ];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<OzmaJack>(),
        ModelDb.Monster<Ozma>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<OzmaJack>().ToMutable(), EastJackSlot),
            (ModelDb.Monster<OzmaJack>().ToMutable(), SouthJackSlot),
            (ModelDb.Monster<OzmaJack>().ToMutable(), WestJackSlot),
            (ModelDb.Monster<OzmaJack>().ToMutable(), NorthJackSlot),
            (ModelDb.Monster<Ozma>().ToMutable(), OzmaSlot)
        ];
    }

    public override float GetCameraScaling() => 0.82f;
}

internal static class OzmaEncounterHelper
{
    public static Ozma? FindBoss(CombatStateLike? combatState) =>
        combatState?.Enemies
            .Select(static creature => creature.Monster)
            .OfType<Ozma>()
            .FirstOrDefault();

    public static IReadOnlyList<Creature> LivingPlayers(CombatStateLike? combatState) =>
        combatState?.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .OrderBy(static creature => creature.CombatId ?? uint.MaxValue)
            .ToArray()
        ?? [];

    public static IReadOnlyList<Creature> LivingAwakeJacks(CombatStateLike? combatState) =>
        combatState?.Enemies
            .Where(static creature => creature.IsAlive
                && creature.Monster is OzmaJack { IsAwake: true })
            .OrderBy(static creature => Array.IndexOf(OzmaElite.JackSlots, creature.SlotName))
            .ToArray()
        ?? [];

    public static OzmaForgottenPower? FindForgottenPower(CombatStateLike? combatState) =>
        combatState?.PlayerCreatures
            .Select(static creature => creature.GetPower<OzmaForgottenPower>())
            .FirstOrDefault(static power => power != null);
}
