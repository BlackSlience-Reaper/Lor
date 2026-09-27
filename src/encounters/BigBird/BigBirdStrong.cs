using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.BigBird;
using LibraryOfRuina.powers.BigBird;
using LibraryOfRuina.visuals.BigBird;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.BigBird;

public sealed class BigBirdStrong : EncounterModel
{
    public const string LeftEyeSlot = "big_bird_left_eye";
    public const string BossSlot = "big_bird_boss";
    public const string RightEyeSlot = "big_bird_right_eye";

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => false;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots =>
    [
        LeftEyeSlot,
        RightEyeSlot,
        BossSlot
    ];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<EyeballBird>(),
        ModelDb.Monster<monsters.BigBird.BigBird>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<EyeballBird>().ToMutable(), LeftEyeSlot),
            (ModelDb.Monster<EyeballBird>().ToMutable(), RightEyeSlot),
            (ModelDb.Monster<monsters.BigBird.BigBird>().ToMutable(), BossSlot)
        ];
    }

    public override float GetCameraScaling() => 0.82f;
}

internal static class BigBirdEncounterHelper
{
    public static bool IsBigBirdEncounter(CombatStateLike? combatState)
    {
        return combatState?.RunState.CurrentRoom is CombatRoom room
            && room.Encounter.MonstersWithSlots.Any(pair =>
                pair.Item1 is monsters.BigBird.BigBird or EyeballBird);
    }

    public static Creature? FindBoss(CombatStateLike? combatState)
    {
        return combatState?.Creatures.FirstOrDefault(creature =>
            creature.IsAlive && creature.Monster is monsters.BigBird.BigBird);
    }

    public static IReadOnlyList<Creature> LivingPlayers(CombatStateLike? combatState)
    {
        return combatState?.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .ToArray()
            ?? [];
    }

    public static IReadOnlyList<Creature> CharmedPlayers(CombatStateLike? combatState)
    {
        return LivingPlayers(combatState)
            .Where(static creature => creature.GetPower<BigBirdCharmedPower>() != null)
            .ToArray();
    }

    public static bool HasCharmedPlayer(CombatStateLike? combatState) =>
        CharmedPlayers(combatState).Count > 0;

    public static Creature? FirstCharmedPlayer(CombatStateLike? combatState) =>
        CharmedPlayers(combatState)
            .OrderBy(static creature => creature.CombatId)
            .FirstOrDefault();

    public static async Task ClearAllCharmedPlayers(PlayerChoiceContext choiceContext, CombatStateLike? combatState)
    {
        foreach (Creature player in CharmedPlayers(combatState))
        {
            await PowerCmdCompat.RemoveIfPresent<BigBirdCharmedPower>(
                player);
        }

        BigBirdFilterOverlay.RefreshForCombat(combatState);
    }
}
