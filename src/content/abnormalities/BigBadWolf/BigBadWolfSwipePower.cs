using System;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace LibraryOfRuina.content.abnormalities.BigBadWolf;

/// <summary>
/// 大灰狼吞下一张牌时挂在狼身上的标记，每名被吞牌的玩家一层实例（<see cref="PowerModel.Target"/> 指向该玩家）。
/// 只负责显示；吞牌、吐出、消化、击杀后以奖励还牌都由 <see cref="monsters.BigBadWolf.BigBadWolf"/> 处理。
/// <para>
/// 原来施加的是原版 <see cref="SwipePower"/>，它的 <c>BeforeDeath</c> 会把牌直接加回牌组、按偷窃草蜢（ThievingHopper）的遭遇发奖励并记
/// “战利品已归还”，与狼自己的还牌重复，只能用跳过型前缀拦掉。本类型没有这个回调，其余与原版逐项相同：
/// 类型、叠加方式、按实例施加、被吞的牌作为额外悬停提示；名称与描述直接用原版 <c>SWIPE_POWER</c> 的本地化键，
/// 各语言与原版显示相同（本模组只提供四语，另起键会让其他语言回退到英文）；图标经 RitsuLib 的
/// <see cref="IModPowerAssetOverrides"/> 取原版同一张图（<c>PowerModel.Icon</c> 按模型 ID 找图集，非虚）。
/// </para>
/// <para>
/// 按 <c>is SwipePower</c> 判断的代码不再认得它：0.111.0 原版只有偷窃草蜢创建 <see cref="SwipePower"/>，没有其他类型判断；
/// 本模组也没有。能力不存档（原版战斗状态只同步 id 与层数），联机两端都用本类型。
/// </para>
/// </summary>
public sealed class BigBadWolfSwipePower : PowerModel, IModPowerAssetOverrides
{
    private CardModel? _stolenCard;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public CardModel? StolenCard
    {
        get => _stolenCard;
        set
        {
            AssertMutable();
            _stolenCard = value;
        }
    }

    private static string VanillaLocKeyPrefix => ModelDb.GetId<SwipePower>().Entry;

    public override LocString Title => new("powers", VanillaLocKeyPrefix + ".title");

    public override LocString Description => new("powers", VanillaLocKeyPrefix + ".description");

    protected override string SmartDescriptionLocKey => VanillaLocKeyPrefix + ".smartDescription";

    protected override string RemoteDescriptionLocKey => VanillaLocKeyPrefix + ".remoteDescription";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        StolenCard == null
            ? Array.Empty<IHoverTip>()
            : [HoverTipFactory.FromCard(StolenCard)];

    // 路径取自原版规范模型，只在显示时求值：ModelDb 初始化期间还拿不到它。
    public PowerAssetProfile AssetProfile => new(CustomIconPath, CustomBigIconPath);

    public string? CustomIconPath => ModelDb.Power<SwipePower>().PackedIconPath;

    public string? CustomBigIconPath => ModelDb.Power<SwipePower>().ResolvedBigIconPath;
}
