using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;

// 以下补丁只转发；规则与三个固定 Boss 入口的说明在 LibraryEncounterWeighting。
// 同一目标上的执行顺序与其他本模组补丁（荣耀楔子、互斥规则）相关：房间序列先在这里重排，再由它们修正。

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
internal static class LibraryEncounterGenerateRoomsPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, Rng rng)
    {
        LibraryEncounterWeighting.AfterActRoomsGenerated(__instance, rng);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
internal static class LibraryEncounterBossWeightPatch
{
    [HarmonyPostfix]
    private static void Postfix(RunManager __instance)
    {
        LibraryEncounterWeighting.AfterRunRoomsGenerated(__instance.DebugOnlyGetState());
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.ValidateRoomsAfterLoad))]
internal static class LibraryEncounterBossAfterLoadPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance)
    {
        LibraryEncounterWeighting.AfterRoomsValidatedAfterLoad(__instance);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateMap))]
internal static class LibraryEncounterBossBeforeMapPatch
{
    [HarmonyPrefix]
    private static void Prefix(RunManager __instance)
    {
        LibraryEncounterWeighting.BeforeMapGenerated(__instance.DebugOnlyGetState());
    }
}
