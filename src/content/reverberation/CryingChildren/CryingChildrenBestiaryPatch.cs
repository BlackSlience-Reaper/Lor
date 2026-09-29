using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.content.acts;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.content.reverberation.CryingChildren;

/// <summary>固定地图遭遇单独加入所属幕的图鉴，保持原生发现和胜利记录规则。</summary>
[HarmonyPatch(typeof(NBestiary), "AddAct")]
internal static class CryingChildrenBestiaryPatch
{

    private static void Postfix(NBestiary __instance, ActModel act)
    {
        if (act is not ReverberationEnsembleAct
            || !SaveManager.Instance.Progress.DiscoveredActs.Contains(act.Id))
        {
            return;
        }
        EncounterModel encounter = ModelDb.Encounter<CryingChildrenEncounter>();
        var entries = new List<BestiaryEntry>();
        foreach (MonsterModel monster in encounter.AllPossibleMonsters)
        {
            entries.Add(BestiaryEntry.FromMonster(monster, encounter, encounter.RoomType));
        }
        VanillaPrivate.BestiaryAddEntries.Invoke(__instance, [entries]);
    }
}
