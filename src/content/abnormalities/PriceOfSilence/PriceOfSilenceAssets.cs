namespace LibraryOfRuina.content.abnormalities.PriceOfSilence;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class PriceOfSilenceAssets
{
    internal const string PriceOfSilenceSfxRoot = "res://audio/sfx/price_of_silence/";
    internal const string PriceOfSilenceStrongBackgroundRoot = "res://images/backgrounds/price_of_silence_strong/";
    internal const string PriceOfSilenceMonsterRoot = "res://images/monsters/price_of_silence/";
    internal const string TimeTraceMonsterRoot = "res://images/monsters/time_trace/";
    internal const string AccumulatedTimePowerIcon = "res://images/powers/accumulated_time_power.png";
    internal const string PassivePowerIcon = "res://images/powers/price_of_silence_passive_power.png";
    internal const string SilencePowerIcon = "res://images/powers/price_of_silence_silence_power.png";
    internal const string YourTimePassivePowerIcon = "res://images/powers/price_of_silence_your_time_passive_power.png";
    internal const string TickingAttackPowerIcon = "res://images/powers/ticking_attack_power.png";
    internal const string TickingGuardPowerIcon = "res://images/powers/ticking_guard_power.png";
    internal const string TimeTraceRestorationPowerIcon = "res://images/powers/time_trace_restoration_power.png";
    internal const string UnstoppableTimePowerIcon = "res://images/powers/unstoppable_time_power.png";
}
