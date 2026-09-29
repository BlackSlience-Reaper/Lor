using LibraryOfRuina.content.guests.DawnOffice;

namespace LibraryOfRuina.patches.visuals;

// 没有外观类可挂 [MonsterVisual] 的登记放在这里（目前只有芬恩）。其余登记都写在各自的外观类上；
// 同一只怪物登记两次会在建表时报错。

/// <summary>芬恩只有一张静态贴图，是目录里唯一的静态贴图登记（<see cref="MonsterVisualCatalog.Validate"/> 会检查）。</summary>
internal static class FinnVisualDeclaration
{
    [MonsterVisual(typeof(Finn), StaticTexture = "res://images/monsters/finn.png")]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145f), new(0.42f, 0.42f), -105f, -299.7f, 105f, 5f, new(0f, -139.8f), new(0f, -333.7f));
}
