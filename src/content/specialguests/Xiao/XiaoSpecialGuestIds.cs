namespace LibraryOfRuina.content.specialguests.Xiao;

public static class XiaoSpecialGuestIds
{
    public const string Guest = "XIAO_SPECIAL_GUEST";
    public const string Event = "XIAO_SPECIAL_GUEST_EVENT";
    public const string StageOneEncounter = "XIAO_SPECIAL_GUEST_STAGE_ONE_ENCOUNTER";
    public const string StageTwoEncounter = "XIAO_SPECIAL_GUEST_STAGE_TWO_ENCOUNTER";

    public const string EventImage = "res://images/events/xiao_special_guest_event.png";
    public const string StageTwoBackground =
        "res://images/special_guests/xiao/backgrounds/xiao_ego_background.png";
    public const string IronLotusBgm =
        "res://audio/special_guests/xiao/bgm/iron_lotus.ogg";

    public const string StoryRoot = "res://images/special_guests/xiao/story/";
    public const string VoiceRoot = "res://audio/special_guests/xiao/story/";
    public const string CombatAudioRoot = "res://audio/special_guests/xiao/combat/";
}

public enum XiaoGuestMove
{
    None = -1,
    LongDrive,
    ThroatPierce,
    Duel,
    FieryDragonSlash,
    FervidEmotion,
    BlazingDance,
    DoubleFlank,
    HotBlood,
    GreatFlame,
    BixueDanxin,
    FlameDragonFist,
    SkywardFlame,
    BreakBamboo,
    JiaotuSuppressEvil,
    BianDispute,
    ChiwenSwallowRidge,
    YaziVengeance,
    SuanniSoaringCloud,
    TaotieFeast,
}
