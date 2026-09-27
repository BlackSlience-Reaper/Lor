using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.monsters.SocialFloorLiberation;

/// <summary>
/// Social-floor values are encounter mechanics, not multiplayer-scaled base
/// statistics. Run after both vanilla HP scaling and LibraryOfRuinaLib's
/// synchronized Chao scaling so initial summons receive their exact values.
/// Loaded remaining HP/Chao is restored later by the encounter state.
/// </summary>
[HarmonyPatch(
    typeof(Creature),
    nameof(Creature.ScaleMonsterHpForMultiplayer))]
[HarmonyAfter("LibraryOfRuinaLib")]
[HarmonyPriority(Priority.Last)]
internal static class SocialFloorLiberationExactVitalsPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        Creature __instance,
        EncounterModel? encounter,
        int playerCount)
    {
        if (encounter is not SocialFloorLiberationEncounter
            || !TryResolveExactVitals(
                __instance.Monster,
                playerCount,
                out int hp,
                out int chao))
        {
            return;
        }

        __instance.SetMaxHpInternal(hp);
        __instance.SetCurrentHpInternal(hp);
        if (__instance is LibraryCreature libraryCreature)
        {
            libraryCreature.SetMaxChaoValueInternal(chao);
            libraryCreature.SetCurrentChaoValueInternal(chao);
        }
    }

    internal static bool TryResolveExactVitals(
        MonsterModel? monster,
        int playerCount,
        out int hp,
        out int chao)
    {
        switch (monster)
        {
            case FalseThrone throne:
                hp = ResolveBossHp(throne.UsesToughValues);
                chao = FalseThrone.ChaoResistance;
                return true;
            case EmeraldCrystal:
                hp = EmeraldCrystal.Hp;
                chao = EmeraldCrystal.ChaoResistance;
                return true;
            case ScowlingFace:
                hp = SocialFloorLiberationEncounter
                    .ResolveFaceHpForPlayerCount(playerCount);
                chao = ScowlingFace.ChaoResistance;
                return true;
            default:
                hp = 0;
                chao = 0;
                return false;
        }
    }

    internal static int ResolveBossHp(bool usesToughValues) =>
        usesToughValues ? FalseThrone.ToughHp : FalseThrone.NormalHp;
}
