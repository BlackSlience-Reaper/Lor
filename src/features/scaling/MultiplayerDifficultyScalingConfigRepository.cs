using System;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.features.scaling;

internal static class MultiplayerDifficultyScalingConfigRepository
{
    public static bool IsEnabled => false;

    public static bool ShouldApplyToEncounter(EncounterModel? encounter) => false;

    public static bool HasPowerOverride(
        Type powerType,
        EncounterModel? encounter,
        MonsterModel? monster,
        int actIndex,
        int playerCount) => false;

    public static decimal ResolveHpMultiplier(
        EncounterModel? encounter,
        MonsterModel? monster,
        int actIndex,
        int playerCount) => 1m;

    public static decimal ResolveBlockMultiplier(
        EncounterModel? encounter,
        MonsterModel? monster,
        int actIndex,
        int playerCount) => 1m;

    public static decimal ResolvePowerMultiplier(
        PowerModel power,
        EncounterModel? encounter,
        MonsterModel? monster,
        int actIndex,
        int playerCount) => 1m;
}
