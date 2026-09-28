using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.specialguests.Rnfmabj;

public sealed class RnfmabjSpecialGuestEncounter :
    EncounterModel,
    ISpecialGuestEncounterStage
{
    public const string LeftHandSlot = "left_hand";
    public const string BodySlot = "rnfmabj";
    public const string RightHandSlot = "right_hand";

    public static readonly Vector2 PlayerLayoutOffset = Vector2.Left * 100f;

    public string SpecialGuestId => RnfmabjSpecialGuestIds.Guest;

    public int SpecialGuestStageIndex => 0;

    public override RoomType RoomType => RoomType.Elite;

    public override bool ShouldGiveRewards => true;

    public override bool HasScene => true;

    public override bool FullyCenterPlayers => true;

    protected override bool HasCustomBackground => true;

    // CombatManager executes enemies in slot order after sorting the roster.
    public override IReadOnlyList<string> Slots =>
        [LeftHandSlot, RightHandSlot, BodySlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Rnfmabj>(),
        ModelDb.Monster<RnfmabjLeftHand>(),
        ModelDb.Monster<RnfmabjRightHand>(),
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        RnfmabjSpecialGuestPresentationAssets.All
            .Concat(Rnfmabj.StaticAssetPaths)
            .Concat(RnfmabjLeftHand.StaticAssetPaths)
            .Concat(RnfmabjRightHand.StaticAssetPaths)
            .Distinct(StringComparer.Ordinal);

    public override float GetCameraScaling() => 0.9f;

    protected override IReadOnlyList<(MonsterModel, string?)>
        GenerateMonsters() =>
    [
        (ModelDb.Monster<Rnfmabj>().ToMutable(), BodySlot),
        (ModelDb.Monster<RnfmabjLeftHand>().ToMutable(), LeftHandSlot),
        (ModelDb.Monster<RnfmabjRightHand>().ToMutable(), RightHandSlot),
    ];
}

[HarmonyPatch(typeof(EncounterModel), "CreateBackgroundAssetsForCustom")]
[HarmonyPriority(Priority.Low)]
[LibraryPatch(Reason = "原版背景标题只能是遭遇 id，本模组背景资源目录另有命名；只作用于 Rnfmabj 特殊来宾遭遇。")]
internal static class RnfmabjSpecialGuestBackgroundAssetsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        EncounterModel __instance,
        Rng rng,
        ref BackgroundAssets __result)
    {
        if (__instance is not RnfmabjSpecialGuestEncounter)
        {
            return true;
        }

        __result = new BackgroundAssets(
            RnfmabjSpecialGuestPresentationAssets.BackgroundTitle,
            rng);
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.AfterAddedToRoom))]
internal static class RnfmabjSpecialGuestBgmAddedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature __instance)
    {
        if (__instance.IsMonster
            && __instance.CombatState?.Encounter
                is RnfmabjSpecialGuestEncounter)
        {
            EncounterBgmController.RegisterMonster(__instance);
        }
    }
}

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.RemoveCreature), typeof(Creature))]
internal static class RnfmabjSpecialGuestBgmRemovedPatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature creature, ref bool __state)
    {
        __state = creature.IsMonster
            && creature.CombatState?.Encounter
                is RnfmabjSpecialGuestEncounter;
        if (__state)
        {
            EncounterBgmController.UnregisterMonster(creature);
        }
    }

    [HarmonyPostfix]
    private static void Postfix(bool __state)
    {
        if (__state)
        {
            Callable.From(StopBgmIfEncounterWasCleared).CallDeferred();
        }
    }

    private static void StopBgmIfEncounterWasCleared()
    {
        CombatState? state = CombatManager.Instance?.DebugOnlyGetState();
        if (state?.Encounter is RnfmabjSpecialGuestEncounter
            && state.Enemies.All(static enemy =>
                enemy.Monster is not RnfmabjMonsterBase))
        {
            EncounterBgmController.StopRuntimeSession();
        }
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.PositionPlayersAndPets))]
internal static class RnfmabjSpecialGuestPlayerPositionPatch
{
    [HarmonyPostfix]
    private static void Postfix(List<NCreature> creatureNodes)
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom
            is not CombatRoom { Encounter: RnfmabjSpecialGuestEncounter })
        {
            return;
        }

        foreach (NCreature node in creatureNodes)
        {
            node.Position += RnfmabjSpecialGuestEncounter.PlayerLayoutOffset;
        }
    }
}
