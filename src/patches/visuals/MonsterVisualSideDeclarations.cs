using Godot;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.monsters.PhilosophyFloorLiberation;
using LibraryOfRuina.reverberation.CryingChildren;
using LibraryOfRuina.reverberation.GearChurch;
using LibraryOfRuina.visuals.PhilosophyFloorLiberation;

namespace LibraryOfRuina.patches;

// 没法把 [MonsterVisual] 写在外观类旁边的登记：芬恩没有外观类；哲学层与残响乐团的外观类不在 visuals/ 的这批文件里。
// 外观类的所有者接手时，把特性（和布局字段）挪到外观类上并删掉这里对应的一条即可，重复登记会在建表时报错。

/// <summary>芬恩只有一张静态贴图，是目录里唯一的静态贴图登记（<see cref="MonsterVisualCatalog.Validate"/> 会检查）。</summary>
internal static class FinnVisualDeclaration
{
    [MonsterVisual(typeof(Finn), StaticTexture = "res://images/monsters/finn.png")]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145f), new(0.42f, 0.42f), -105f, -299.7f, 105f, 5f, new(0f, -139.8f), new(0f, -333.7f));
}

internal static class PhilosophyFloorTwilightVisualDeclaration
{
    [MonsterVisual(typeof(PhilosophyFloorTwilight), Visuals = typeof(PhilosophyFloorTwilightCreatureVisuals))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -216f), new(0.45f, 0.45f), -315f, -624f, 315f, 12f, new(0f, -202f), new(36f, -508f))
    {
        TalkPos = new Vector2(0f, -540f),
        StateDisplayLiftY = 52f,
    };
}

[MonsterVisual(typeof(ReverberationEileen), Visuals = typeof(ReverberationEileenVisuals), ScenePath = GearChurchAssets.EileenScene)]
[MonsterVisual(typeof(GearChurchFollower), Visuals = typeof(GearChurchFollowerVisuals), ScenePath = GearChurchAssets.FollowerScene)]
[MonsterVisual(typeof(ReverberationPhilip), Visuals = typeof(ReverberationPhilipVisuals), ScenePath = CryingChildrenAssets.PhilipScene)]
[MonsterVisual(typeof(UnspeakingChild), Visuals = typeof(UnspeakingChildVisuals), ScenePath = CryingChildrenAssets.ChildScene)]
internal static class ReverberationVisualDeclarations
{
}
