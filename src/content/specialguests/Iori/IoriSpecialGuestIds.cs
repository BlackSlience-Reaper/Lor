namespace LibraryOfRuina.content.specialguests.Iori;

public static class IoriSpecialGuestIds
{
    public const string Guest = "IORI_SPECIAL_GUEST";
    public const string Event = "IORI_SPECIAL_GUEST_EVENT";
    public const string StageOneEncounter =
        "IORI_SPECIAL_GUEST_STAGE_ONE_ENCOUNTER";
    public const string StageTwoEncounter =
        "IORI_SPECIAL_GUEST_STAGE_TWO_ENCOUNTER";

    public const string EventImage =
        "res://images/events/iori_special_guest_event.png";
    public const string CombatAudioRoot =
        "res://audio/special_guests/iori/combat/";

    public const string SnapshotValueKey = "iori.reception-snapshot";
    public const string StageOneLayerValueKey = "iori.stage-one-layer";
    public const string StageTwoLayerValueKey = "iori.stage-two-layer";
}

public enum IoriStance
{
    None = -1,
    Slash = 0,
    Pierce = 1,
    Blunt = 2,
    Defense = 3,
}

public enum IoriMove
{
    None = -1,
    StanceShift,
    SnakeSwordplay,
    SlitheringCut,
    VioletSword,
    PreyLock,
    FangPenetration,
    PenetratingWound,
    SwiftDownwardStrike,
    PythonImpact,
    DuelDance,
    EndlessFlow,
    ScaledBarrier,
    NoEscape,
    PhantomDance,
}
