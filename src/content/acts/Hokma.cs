using Godot;
using LibraryOfRuina.content.liberation.Religion;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;

namespace LibraryOfRuina.content.acts;

public sealed class Hokma : LibraryOfRuinaActModel
{
    internal override string MapTopBackground => "res://images/packed/map/map_bgs/hokma/map_top_hokma.png";

    internal override string MapMiddleBackground => "res://images/packed/map/map_bgs/hokma/map_middle_hokma.png";

    internal override string MapBottomBackground => "res://images/packed/map/map_bgs/hokma/map_bottom_hokma.png";

    public override ActModel TemplateAct => ModelDb.Act<Glory>();

    // 宗教层已通行路线使用深灰色，与浅色纸面形成清晰对比。
    public override Color MapTraveledColor => new("454545");

    // 宗教层未通行路线使用中灰色，保留与已通行路线的明暗区别。
    public override Color MapUntraveledColor => new("707070");

    // 普通节点与解放战徽记共用贴近地图纸面的暖灰色描边。
    public override Color MapBgColor => new("B6B2A7");

    protected override EncounterModel FixedBoss => ModelDb.Encounter<ReligionFloorLiberationEncounter>();
}
