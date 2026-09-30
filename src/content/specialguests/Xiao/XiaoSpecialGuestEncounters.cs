using System;
using System.Linq;
using HarmonyLib;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.infra.lifecycle;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.ui.scene_transitions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.specialguests.Xiao;

public sealed class XiaoSpecialGuestStageOneEncounter :
    EncounterModel,
    ISpecialGuestEncounterStage
{
    public string SpecialGuestId => XiaoSpecialGuestIds.Guest;

    public int SpecialGuestStageIndex => 0;

    // Intermediate reception stages keep their normal reward tier.
    public override RoomType RoomType => RoomType.Monster;

    public override bool ShouldGiveRewards => true;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => ["miris", "xiao"];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<XiaoStageOne>(),
        ModelDb.Monster<Miris>(),
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths
            .Concat(XiaoSpecialGuestAssets.StageOneReceptionBgms)
            .Concat(XiaoSpecialGuestAssets.StageOneXiao)
            .Concat(XiaoSpecialGuestAssets.Miris)
            .Distinct(StringComparer.Ordinal);

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Miris>().ToMutable(), "miris"),
        (ModelDb.Monster<XiaoStageOne>().ToMutable(), "xiao"),
    ];
}

public sealed class XiaoSpecialGuestStageTwoEncounter :
    EncounterModel,
    ISpecialGuestEncounterStage
{
    public string SpecialGuestId => XiaoSpecialGuestIds.Guest;

    public int SpecialGuestStageIndex => 1;

    // The last stage of a multi-stage reception is the elite battle.
    public override RoomType RoomType => RoomType.Elite;

    public override bool ShouldGiveRewards => true;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => ["xiao"];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<XiaoEgo>()];

    public override IEnumerable<string> ExtraAssetPaths =>
        GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths
            .Concat(XiaoSpecialGuestAssets.StageTwoXiao)
            .Concat(LorexSceneTransitionAssetPaths.All)
            .Distinct(StringComparer.Ordinal);

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<XiaoEgo>().ToMutable(), "xiao")];
}

[HarmonyPatch(typeof(EncounterModel), "CreateBackgroundAssetsForCustom")]
[HarmonyPriority(Priority.Low)]
[LibraryPatch(Reason = "原版背景标题只能是遭遇 id，晓两阶段需要共用按种子抽取的接待层；只作用于晓特殊来宾遭遇。")]
internal static class XiaoSpecialGuestBackgroundAssetsPatch
{
    internal const string StageOneLayerValueKey = "xiao.stage-one-layer";

    [HarmonyPrefix]
    private static bool Prefix(
        EncounterModel __instance,
        Rng rng,
        ref BackgroundAssets __result)
    {
        if (__instance is not (XiaoSpecialGuestStageOneEncounter
            or XiaoSpecialGuestStageTwoEncounter))
        {
            return true;
        }

        IRunState? runState = CurrentRun.State;
        SpecialGuestRunStateModifier? state =
            SpecialGuestRunStateModifier.TryGet(runState);
        string? selectedLayer = state?.GetValue(StageOneLayerValueKey);
        if (string.IsNullOrWhiteSpace(selectedLayer)
            || !GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths
                .Contains(selectedLayer, StringComparer.Ordinal))
        {
            ulong seed = runState?.Rng.Seed ?? 0UL;
            ulong roll = SpecialGuestRegistry.StableHash64(
                seed + "|" + XiaoSpecialGuestIds.Guest + "|stage-background");
            selectedLayer = GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths[
                (int)(roll % (ulong)GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths.Length)];
            state?.SetValue(StageOneLayerValueKey, selectedLayer);
        }

        __result = new BackgroundAssets(
            GuestReceptionPoolRegistry.SharedBackgroundTitle,
            rng);
        __result.BgLayers.Clear();
        __result.BgLayers.Add(selectedLayer);
        return false;
    }
}
