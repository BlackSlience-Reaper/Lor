// #nullable enable
// using System.Linq;
// using HarmonyLib;
// using LibraryOfRuina.features.settings;
// using MegaCrit.Sts2.Core.Models;
// using MegaCrit.Sts2.Core.Models.Acts;
//
// namespace LibraryOfRuina.patches.Yanami;
//
// [HarmonyPatch(typeof(Hive), nameof(Hive.AllAncients), MethodType.Getter)]
// public static class HiveYanamiAncientPatch
// {
//     [HarmonyPostfix]
//     public static void Postfix(ref IEnumerable<AncientEventModel> __result)
//     {
//         if (!LibraryOfRuinaSettings.MonsterExtensionEnabled)
//         {
//             return;
//         }
//
//         __result = __result
//             .Concat([ModelDb.AncientEvent<monsters.Yanami.Yanami>()])
//             .Distinct();
//     }
// }
