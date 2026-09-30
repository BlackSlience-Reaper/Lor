namespace LibraryOfRuina.content.abnormalities.Ozma;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class OzmaAssets
{
    internal const string OzmaSfxRoot = "res://audio/sfx/ozma/";
    internal const string OzmaMonsterRoot = "res://images/monsters/ozma/";
    internal const string EastJackPowerIcon = "res://images/powers/ozma_east_jack_power.png";
    internal const string ForgottenPowerIcon = "res://images/powers/ozma_forgotten_power.png";
    internal const string LostMemoryPowerIcon = "res://images/powers/ozma_lost_memory_power.png";
    internal const string NorthJackPowerIcon = "res://images/powers/ozma_north_jack_power.png";
    internal const string PainPassivePowerIcon = "res://images/powers/ozma_pain_passive_power.png";
    internal const string SorrowPassivePowerIcon = "res://images/powers/ozma_sorrow_passive_power.png";
    internal const string SouthJackPowerIcon = "res://images/powers/ozma_south_jack_power.png";
    internal const string TakeOrBeTakenPowerIcon = "res://images/powers/ozma_take_or_be_taken_power.png";
    internal const string WestJackPowerIcon = "res://images/powers/ozma_west_jack_power.png";
    internal const string WhichIsRealPowerIcon = "res://images/powers/ozma_which_is_real_power.png";
    internal const string OblivionMeetingTexture = "res://images/vfx/ozma_oblivion_meeting.png";
}
