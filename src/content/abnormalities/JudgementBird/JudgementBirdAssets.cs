using LibraryOfRuina.framework.assets;

namespace LibraryOfRuina.content.abnormalities.JudgementBird;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class JudgementBirdAssets
{
    internal const string JudgementBirdSfxRoot = "res://audio/sfx/judgement_bird/";
    internal const string EscapedBirdMonsterRoot = "res://images/monsters/escaped_bird/";
    internal const string JudgementBirdMonsterRoot = "res://images/monsters/judgement_bird/";
    internal const string EscapedBirdScene = "res://scenes/creature_visuals/escaped_bird.tscn";
    internal const string JudgementBirdScene = "res://scenes/creature_visuals/judgement_bird.tscn";
    internal const string JudgementBirdJudgementVideo = "res://videos/judgement_bird_judgement.ogv";
    internal const string LibraryPassiveGreenIcon = SharedAssets.LibraryPassiveGreenIcon;
}
