using System;
using System.Linq;

namespace LibraryOfRuina.specialguests.Iori;

public sealed class IoriStageOne : IoriMonsterBase
{
    protected override bool IsSecondStage => false;
}

public sealed class IoriStageTwo : IoriMonsterBase
{
    protected override bool IsSecondStage => true;
}

internal static class IoriSpecialGuestAssets
{
    internal static readonly string[] CombatAudio =
    [
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Slash_Hori.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Slash_VertDown.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Slash_VertUp.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Stab_Stab1.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Stab_Stab2.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Hit_Hori.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Hit_Vert.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Guard.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Warp.ogg",
    ];

    internal static readonly string[] PowerIcons =
    [
        "res://images/powers/library_passive_orange.png",
        "res://images/powers/iori_probability_fluctuation_passive_power.png",
        "res://images/powers/iori_dimensional_walk_passive_power.png",
        "res://images/powers/iori_stance_shift_passive_power.png",
        "res://images/powers/iori_card_play_pain_power.png",
        "res://images/powers/iori_slash_stance_power.png",
        "res://images/powers/iori_pierce_stance_power.png",
        "res://images/powers/iori_blunt_stance_power.png",
        "res://images/powers/iori_defense_stance_power.png",
    ];

    internal static readonly string[] All =
        IoriPresentationAssets.All
        .Concat(PowerIcons)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}
