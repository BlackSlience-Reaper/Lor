using System;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.framework.cards;

/// <summary>
/// 异想体书页遗物三选一界面上的选择卡。只作为选项出现：不进卡牌图鉴、不会在战斗中生成，也不会被打出。
/// 卡的模型 ID 仍由具体类名决定；<see cref="PageMode"/> 是这张卡在对应书页遗物上选中的模式。
/// 同一模式枚举的选择卡可以被多个遗物共用（例如普通书页与魔法少女补齐的强化书页），界面上用
/// <see cref="IPageChoiceCard"/> 识别“这是书页选择”，以替换横幅文字、隐藏卡牌类型牌。
/// </summary>
public abstract class PageChoiceCard<TMode> : CardModel, IPageChoiceCard
    where TMode : struct, Enum
{
    protected PageChoiceCard()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public abstract TMode PageMode { get; }

    /// <summary><c>images/packed/card_portraits/colorless/</c> 下的文件名（含扩展名）。</summary>
    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    /// <summary>
    /// 选中这张卡对应的模式。选项都由遗物自己创建，同一枚举的别家选择卡不会出现在它的界面上；
    /// 其他卡（或跳过时的 null）按意外输入抛出。
    /// </summary>
    internal static TMode ModeOf(CardModel? card) =>
        card is PageChoiceCard<TMode> choiceCard
            ? choiceCard.PageMode
            : throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card);
}

internal interface IPageChoiceCard
{
}
