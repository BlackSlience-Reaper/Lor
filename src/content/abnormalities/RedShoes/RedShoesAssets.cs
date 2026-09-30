namespace LibraryOfRuina.content.abnormalities.RedShoes;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class RedShoesAssets
{
    internal const string LeftShoeTexture = "res://images/monsters/red_shoes/left_shoe.png";
    internal const string LeftShoeAttackTexture = "res://images/monsters/red_shoes/left_shoe_attack.png";
    internal const string LeftShoeHitTexture = "res://images/monsters/red_shoes/left_shoe_hit.png";
    internal const string LeftShoeParryTexture = "res://images/monsters/red_shoes/left_shoe_parry.png";
    internal const string RightShoeTexture = "res://images/monsters/red_shoes/right_shoe.png";
    internal const string RightShoeAttackTexture = "res://images/monsters/red_shoes/right_shoe_attack.png";
    internal const string RightShoeHitTexture = "res://images/monsters/red_shoes/right_shoe_hit.png";
}
