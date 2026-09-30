namespace LibraryOfRuina.content.abnormalities.BurrowingHeaven;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class BurrowingHeavenAssets
{
    internal const string BurrowingHeavenSfxRoot = "res://audio/sfx/burrowing_heaven/";
    internal const string BurrowingHeavenMonsterRoot = "res://images/monsters/burrowing_heaven/";
    internal const string HeavenThornMonsterRoot = "res://images/monsters/heaven_thorn/";
    internal const string InCognitionPassivePowerIcon = "res://images/powers/burrowing_heaven_in_cognition_passive_power.png";
    internal const string PerfectFocusPassivePowerIcon = "res://images/powers/burrowing_heaven_perfect_focus_passive_power.png";
    internal const string SleepPowerIcon = "res://images/powers/burrowing_heaven_sleep_power.png";
    internal const string WingsTowardOldGodPassivePowerIcon = "res://images/powers/burrowing_heaven_wings_toward_old_god_passive_power.png";
    internal const string HeavenThornDoNotShiftGazePassivePowerIcon = "res://images/powers/heaven_thorn_do_not_shift_gaze_passive_power.png";
    internal const string HeavenThornInvisibleConnectionPassivePowerIcon = "res://images/powers/heaven_thorn_invisible_connection_passive_power.png";
    internal const string HeavenThornSleepPowerIcon = "res://images/powers/heaven_thorn_sleep_power.png";
}
