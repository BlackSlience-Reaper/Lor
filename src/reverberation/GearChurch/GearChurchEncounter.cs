using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.acts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using static LibraryOfRuina.reverberation.GearChurch.GearChurchRules;

namespace LibraryOfRuina.reverberation.GearChurch;

public sealed class GearChurchEncounter : ReverberationEncounterModel
{
    internal const string EileenSlot = "eileen";
    internal static readonly string[] FollowerSlots = ["follower_1", "follower_2", "follower_3"];

    [SavedProperty]
    public int LastFollowerPlanRound { get; private set; } = -1;

    protected override IReadOnlyList<string> EnemySlots => [EileenSlot, .. FollowerSlots];

    protected override bool HasCustomBackground => true;

    public override float GetCameraScaling() => 0.80f;

    public override Godot.Vector2 GetCameraOffset() => new(-35f, 40f);

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<ReverberationEileen>(), ModelDb.Monster<GearChurchFollower>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var monsters = new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<ReverberationEileen>().ToMutable(), EileenSlot)
        };
        for (int i = 0; i < OpeningFollowers; i++)
        {
            monsters.Add((ModelDb.Monster<GearChurchFollower>().ToMutable(), FollowerSlots[i]));
        }
        return monsters;
    }

    internal static GearChurchFollower[] Followers(CombatStateLike state) =>
        state.Enemies
            .Where(creature => creature.IsAlive)
            .OrderBy(creature => creature.CombatId)
            .Select(creature => creature.Monster)
            .OfType<GearChurchFollower>()
            .ToArray();

    internal async Task SpawnFollowers(CombatStateLike state, int requested)
    {
        int count = Math.Min(requested, MaximumFollowers - Followers(state).Length);
        foreach (string slot in FollowerSlots)
        {
            if (count <= 0)
            {
                break;
            }
            if (state.Creatures.Any(creature => creature.IsAlive && creature.SlotName == slot))
            {
                continue;
            }
            await CreatureCmd.Add(ModelDb.Monster<GearChurchFollower>().ToMutable(), state,
                CombatSide.Enemy, slot);
            count--;
        }
    }

    internal async Task PrepareFollowers(CombatStateLike state)
    {
        if (LastFollowerPlanRound == state.RoundNumber)
        {
            return;
        }
        LastFollowerPlanRound = state.RoundNumber;
        GearChurchFollower[] followers = Followers(state);
        GearChurchMove[] pool =
        [
            GearChurchMove.Guidance, GearChurchMove.DefenseInstruction,
            GearChurchMove.Assault, GearChurchMove.SteamEruption
        ];
        var assignments = new List<GearChurchMove[]>();
        CollectAssignments(0, new GearChurchMove[followers.Length]);
        if (assignments.Count == 0 || followers.Length == 0)
        {
            return;
        }
        GearChurchMove[] chosen = assignments[state.RunState.Rng.MonsterAi.NextInt(assignments.Count)];
        for (int i = 0; i < followers.Length; i++)
        {
            await followers[i].PrepareRound(state.RoundNumber, chosen[i]);
        }

        void CollectAssignments(int index, GearChurchMove[] plan)
        {
            if (index == followers.Length)
            {
                assignments.Add((GearChurchMove[])plan.Clone());
                return;
            }
            foreach (GearChurchMove move in pool)
            {
                if ((int)move == followers[index].LastPerformedMove || plan.Take(index).Contains(move))
                {
                    continue;
                }
                plan[index] = move;
                CollectAssignments(index + 1, plan);
            }
        }
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
internal static class GearChurchTechnologyReceptionPatch
{
    private static bool Prefix(ActModel __instance, RoomType roomType, ref EncounterModel __result)
    {
        RunState? run = RunManager.Instance.DebugOnlyGetState();
        if (__instance is ReverberationEnsembleAct
            && ReferenceEquals(run?.Act, __instance)
            && roomType == RoomType.Elite
            && run?.CurrentMapCoord is { } coord
            && ReverberationEnsembleActMap.GetReceptionKey(coord) == "TECHNOLOGY")
        {
            __result = ModelDb.Encounter<GearChurchEncounter>();
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(NBestiary), "AddAct")]
internal static class GearChurchBestiaryPatch
{
    private static readonly MethodInfo AddEntries = AccessTools.Method(typeof(NBestiary), "AddEntries");

    private static void Postfix(NBestiary __instance, ActModel act)
    {
        if (act is not ReverberationEnsembleAct
            || !SaveManager.Instance.Progress.DiscoveredActs.Contains(act.Id))
        {
            return;
        }
        EncounterModel encounter = ModelDb.Encounter<GearChurchEncounter>();
        var entries = encounter.AllPossibleMonsters
            .Select(monster => BestiaryEntry.FromMonster(monster, encounter, encounter.RoomType))
            .ToList();
        AddEntries.Invoke(__instance, [entries]);
    }
}
