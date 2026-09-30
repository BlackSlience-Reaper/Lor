using ActLikeIt2;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Unlocks;

namespace LibraryOfRuina.content.acts;

/// <summary>
/// 残响乐团接待地图。接待节点复用第三幕精英池，地图图标保留楼层身份。
/// </summary>
public sealed class ReverberationEnsembleAct : TemplateActModel
{
    // 残响乐团在选幕界面中的幕次，从一开始计数。
    internal const int ActNumber = 4;

    // 除先古与最终 Boss 外，固定包含三层接待和三层补给。
    internal const int InteriorFloorCount = 6;

    internal const string MapAssetRoot =
        LibraryActAssets.ReverberationEnsembleRoot;

    internal const string BossIconPath =
        LibraryActAssets.BlueReverberationTexture;

    internal const string BossOutlineShaderPath =
        LibraryActAssets.ReverberationBossOutlineTexture;

    public override ActModel TemplateAct => ModelDb.Act<Glory>();

    public override int Index => ActNumber - 1;

    protected override int BaseNumberOfRooms => InteriorFloorCount;

    // 接待节点均为精英，普通遭遇池不额外预留弱小遭遇。
    protected override int NumberOfWeakEncounters => 0;

    public override Color MapTraveledColor => new("203D58");

    public override Color MapUntraveledColor => new("52697E");

    // 节点、先古及苍蓝残响的外描边匹配地图的灰蓝纸面。
    public override Color MapBgColor => new("91A1B1");

    public override bool IsUnlocked(UnlockState unlockState) => true;
}
