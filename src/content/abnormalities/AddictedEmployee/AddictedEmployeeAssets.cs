namespace LibraryOfRuina.content.abnormalities.AddictedEmployee;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class AddictedEmployeeAssets
{
    internal const string SongMachineAttackSfx = "res://audio/sfx/song_machine/song_machine_attack.ogg";
    internal const string AddictedEmployeeMonsterPrefix = "res://images/monsters/addicted_employee/addicted_employee_";
    // Spine 身体（tools/spine_from_sprite 生成，按躯干、头、手臂分层）：骨骼与图集以原始文件打进 PCK，运行时按路径加载
    internal const string AddictedEmployeeSpineAtlas = "res://images/monsters/addicted_employee/addicted_employee.atlas";
    internal const string AddictedEmployeeSpineSkeleton = "res://images/monsters/addicted_employee/addicted_employee.spine-json";
}
