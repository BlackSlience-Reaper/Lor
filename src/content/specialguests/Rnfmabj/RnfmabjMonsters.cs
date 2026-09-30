namespace LibraryOfRuina.content.specialguests.Rnfmabj;

public sealed class RnfmabjLeftHand : RnfmabjHandBase
{
    public static readonly string[] StaticAssetPaths = RnfmabjCombatAssets.All;

    protected override bool IsLeftHand => true;

    public override IEnumerable<string> AssetPaths => StaticAssetPaths;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        new()
        {
            Blunt = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Endure,
            Slash = LibraryResistanceLevel.Normal,
        };
}

public sealed class RnfmabjRightHand : RnfmabjHandBase
{
    public static readonly string[] StaticAssetPaths = RnfmabjCombatAssets.All;

    protected override bool IsLeftHand => false;

    public override IEnumerable<string> AssetPaths => StaticAssetPaths;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        new()
        {
            Blunt = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Slash = LibraryResistanceLevel.Endure,
        };
}

internal static class RnfmabjCombatAssets
{
    private const string AudioRoot = RnfmabjSpecialGuestIds.CombatAudioRoot;

    public static readonly string[] All =
    [
        "res://images/powers/rnfmabj_corrosion_power.png",
        "res://images/powers/rnfmabj_counter_evade_power.png",
        "res://images/powers/rnfmabj_hand_mechanics_power.png",
        "res://images/powers/rnfmabj_mechanics_power.png",
        "res://images/powers/rnfmabj_twisted_blade_passive_power.png",
        AudioRoot + "Yan_GreatSword_Finish.ogg",
        AudioRoot + "Yan_GreatSword_Start.ogg",
        AudioRoot + "Yan_Guard.ogg",
        AudioRoot + "Yan_Lib_Hori.ogg",
        AudioRoot + "Yan_Lib_Vert.ogg",
        AudioRoot + "Yan_Stab.ogg",
        AudioRoot + "Yan_Stigma_Atk.ogg",
        AudioRoot + "Yan_Stigma_Start.ogg",
        AudioRoot + "Yan_Typing_Atk.ogg",
        AudioRoot + "Yan_Typing_Start.ogg",
        AudioRoot + "Yan_Vert.ogg",
    ];
}
