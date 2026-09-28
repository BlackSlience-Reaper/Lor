using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.acts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.reverberation.CryingChildren;

public sealed class CryingChildrenEncounter : ReverberationEncounterModel
{
    internal const string PhilipSlot = "philip";

    internal static readonly string[] ChildSlots = ["child_1", "child_2", "child_3"];

    [SavedProperty]
    public int LastChildrenPlanRound { get; private set; } = -1;

    protected override IReadOnlyList<string> EnemySlots => [PhilipSlot, .. ChildSlots];

    protected override bool HasCustomBackground => true;

    public override float GetCameraScaling() => 0.82f;

    public override Godot.Vector2 GetCameraOffset() => new(-70f, 45f);

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<ReverberationPhilip>(), ModelDb.Monster<UnspeakingChild>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<ReverberationPhilip>().ToMutable(), PhilipSlot)];

    internal async Task SpawnChildren(CombatStateLike state)
    {
        foreach (string slot in ChildSlots)
        {
            // 存档恢复或重复回调以已存在槽位为准，避免重复召唤。
            if (state.Creatures.Any(creature => creature.SlotName == slot))
            {
                continue;
            }
            var model = (UnspeakingChild)ModelDb.Monster<UnspeakingChild>().ToMutable();
            model.MarkSpawned(state.RoundNumber);
            await CreatureCmd.Add(model, state, CombatSide.Enemy, slot);
        }
    }

    internal async Task PrepareChildren(CombatStateLike state)
    {
        if (LastChildrenPlanRound == state.RoundNumber)
        {
            return;
        }
        UnspeakingChild[] children = state.Enemies
            .Where(creature => creature.IsAlive)
            .OrderBy(creature => creature.CombatId)
            .Select(creature => creature.Monster)
            .OfType<UnspeakingChild>()
            .ToArray();
        if (children.Length == 0)
        {
            return;
        }
        LastChildrenPlanRound = state.RoundNumber;
        var moves = new List<CryingMove>
            { CryingMove.Murmur, CryingMove.FoulWings, CryingMove.EndlessTorment };
        foreach (UnspeakingChild child in children)
        {
            int index = state.RunState.Rng.MonsterAi.NextInt(moves.Count);
            CryingMove move = moves[index];
            moves.RemoveAt(index);
            await child.PrepareRound(state.RoundNumber, move);
        }
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
[LibraryPatch(Reason = "ActModel.PullNextEncounter 非虚无 Hook；只在本模组残响乐团幕的历史层接待精英房返回哭泣之子。可改后缀，但原版 getter 在精英池为空时会除零，暂不改。")]
internal static class CryingChildrenHistoryReceptionPatch
{
    private static bool Prefix(ActModel __instance, RoomType roomType, ref EncounterModel __result)
    {
        RunState? run = RunManager.Instance.DebugOnlyGetState();
        if (__instance is ReverberationEnsembleAct
            && ReferenceEquals(run?.Act, __instance)
            && roomType == RoomType.Elite
            && run?.CurrentMapCoord is { } coord
            && ReverberationEnsembleActMap.GetReceptionKey(coord) == "HISTORY")
        {
            __result = ModelDb.Encounter<CryingChildrenEncounter>();
            return false;
        }
        return true;
    }
}
