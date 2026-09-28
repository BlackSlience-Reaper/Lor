using System;
using System.Threading.Tasks;
using LibraryOfRuina.cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.relics;

/// <summary>
/// 获得时三选一的异想体书页遗物。模式枚举约定 <c>None = 0</c> 表示尚未选择，其余成员都是可选模式；
/// 选择卡继承 <see cref="PageChoiceCard{TMode}"/>，由卡自己声明对应的模式。
/// <para>
/// 模式本身必须仍是具体类上的 <c>[SavedProperty] Mode</c>：SavedProperty 的声明类型决定存档键与联机 net-id，
/// 挪进这个泛型基类会改变两者。基类只通过 <see cref="SelectedMode"/> 读写它。
/// </para>
/// <para>
/// 两条选择路径：奖励界面上的预选（<see cref="AbnormalityPageRewardPreselection"/>，获得前选好模式，
/// 跳过则奖励保留）走 <see cref="ApplyPreselectedMode"/>；其余获得方式在 <see cref="AfterObtained"/> 里选，
/// 跳过则移除刚获得的遗物并把本次选择记为未拾取，走 <see cref="ApplyObtainedChoiceAsync"/>。
/// 两条路径对同一遗物的副作用本来就不同，子类各自覆写时要分别对照原实现。
/// </para>
/// </summary>
public abstract class ModalPageRelic<TMode> : LibraryRelicModel, IModalPageRelic
    where TMode : struct, Enum
{
    /// <summary>具体类 <c>[SavedProperty] Mode</c> 的读写入口。</summary>
    protected abstract TMode SelectedMode { get; set; }

    /// <summary>三张选择卡，顺序即界面顺序。只在有 <see cref="RelicModel.Owner"/> 时调用。</summary>
    protected abstract IReadOnlyList<CardModel> CreateModeChoiceCards();

    /// <summary>把模式同步到 <c>DynamicVars["Mode"]</c>、遗物状态与计数显示。</summary>
    protected abstract void UpdateModeUiState();

    /// <summary>
    /// 切换或回退模式时是否通知图标变化并刷新背包里的图标节点。
    /// 迁移前有一部分书页遗物从不刷新图标（它们的图标不随模式变化）；保持各自原来的做法。
    /// </summary>
    protected virtual bool RefreshIconOnModeChange => true;

    /// <summary>
    /// 获得时是否在判断“已有模式”之前先刷新一次界面状态（迁移前两种写法都有，区别只在尚未选择时多刷新一次）。
    /// </summary>
    protected virtual bool RefreshUiBeforeModeChoice => false;

    /// <summary>读档后模式非法时回退到的模式：枚举里值为 1 的成员，即第一个可选模式。</summary>
    protected virtual TMode FallbackMode => (TMode)Enum.ToObject(typeof(TMode), 1);

    /// <summary><see cref="SetMode"/> 写入新模式之后、刷新界面之前清理与模式相关的状态。</summary>
    protected virtual void ResetStateOnModeSet(TMode mode)
    {
    }

    /// <summary><see cref="FallbackToDefaultModeAfterLoad"/> 写入回退模式之后、记日志之前清理状态。</summary>
    protected virtual void ResetStateOnFallback()
    {
    }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(SelectedMode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (RefreshUiBeforeModeChoice)
        {
            UpdateModeUiState();
        }

        if (!IsNoneMode(SelectedMode))
        {
            if (!RefreshUiBeforeModeChoice)
            {
                UpdateModeUiState();
            }

            if (RefreshIconOnModeChange)
            {
                RefreshInventoryIcon();
            }

            return;
        }

        IReadOnlyList<CardModel> options = CreateModeChoiceCards();
        CardModel? chosenCard = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            options,
            Owner,
            canSkip: true);

        if (chosenCard == null)
        {
            await AbnormalityPageRewardHelper.SkipObtainedPageRelic(this, nameof(AfterObtained));
            return;
        }

        await ApplyObtainedChoiceAsync(ResolveModeFromChoiceCard(chosenCard));
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        if (RefreshIconOnModeChange)
        {
            RefreshInventoryIcon();
        }

        return Task.CompletedTask;
    }

    /// <summary><see cref="AfterObtained"/> 里选定模式之后的处理。拾取时的一次性效果在这里接在 <see cref="SetMode"/> 之后。</summary>
    protected virtual Task ApplyObtainedChoiceAsync(TMode mode)
    {
        SetMode(mode);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 奖励预选选定模式。之后遗物以已有模式获得，<see cref="AfterObtained"/> 只刷新界面，
    /// 拾取效果由 <see cref="AbnormalityPagePostObtainEffectAttribute"/> 标记的方法在获得后执行。
    /// </summary>
    protected virtual void ApplyPreselectedMode(TMode mode) => SetMode(mode);

    /// <summary>
    /// 只写模式并刷新图标与界面，不清状态、不刷新背包图标节点。原来没有同步 <c>SetMode</c> 的遗物，预选路径就是这样处理的。
    /// </summary>
    protected void AssignPreselectedModeOnly(TMode mode)
    {
        SelectedMode = mode;
        RelicIconChanged();
        UpdateModeUiState();
    }

    protected virtual void SetMode(TMode mode)
    {
        SelectedMode = mode;
        ResetStateOnModeSet(mode);
        RefreshModePresentation();
    }

    protected virtual void EnsureValidModeOrFallback(string context)
    {
        if (IsConcreteMode(SelectedMode))
        {
            return;
        }

        FallbackToDefaultModeAfterLoad(context);
    }

    protected virtual void FallbackToDefaultModeAfterLoad(string context)
    {
        TMode oldMode = SelectedMode;
        SelectedMode = FallbackMode;
        ResetStateOnFallback();
        Log.Warn("[LibraryOfRuina.PageRelic] "
            + GetType().Name
            + " recovered loaded Mode "
            + Convert.ToInt32(oldMode)
            + " during "
            + context
            + "; fallback to "
            + FallbackMode
            + ".");
        RefreshModePresentation();
    }

    protected void RefreshInventoryIcon() => PageRelicInventoryIcon.Refresh(this);

    protected static TMode ResolveModeFromChoiceCard(CardModel? card) => PageChoiceCard<TMode>.ModeOf(card);

    protected static bool IsKnownMode(TMode mode) => Enum.IsDefined(mode);

    protected static bool IsConcreteMode(TMode mode) => IsKnownMode(mode) && !IsNoneMode(mode);

    private static bool IsNoneMode(TMode mode) => Convert.ToInt32(mode) == 0;

    private void RefreshModePresentation()
    {
        if (!RefreshIconOnModeChange)
        {
            UpdateModeUiState();
            return;
        }

        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    bool IModalPageRelic.HasSelectedMode => !IsNoneMode(SelectedMode);

    IReadOnlyList<CardModel> IModalPageRelic.CreateModeChoiceCards() => CreateModeChoiceCards();

    void IModalPageRelic.ApplyPreselectedChoice(CardModel chosenCard) =>
        ApplyPreselectedMode(ResolveModeFromChoiceCard(chosenCard));
}

/// <summary>奖励预选用的非泛型入口，由 <see cref="ModalPageRelic{TMode}"/> 与强化书页遗物实现。</summary>
internal interface IModalPageRelic
{
    bool HasSelectedMode { get; }

    IReadOnlyList<CardModel> CreateModeChoiceCards();

    void ApplyPreselectedChoice(CardModel chosenCard);
}

internal static class PageRelicInventoryIcon
{
    /// <summary>模式改变图标后，背包里已经创建的节点不会自己重新取图，需要手动换纹理。</summary>
    public static void Refresh(RelicModel relic)
    {
        NRelicInventory? inventory = NRun.Instance?.GlobalUi?.RelicInventory;
        if (inventory == null)
        {
            return;
        }

        foreach (NRelicInventoryHolder holder in inventory.RelicNodes)
        {
            if (!ReferenceEquals(holder.Relic.Model, relic))
            {
                continue;
            }

            holder.Relic.Icon.Texture = relic.Icon;
            holder.Relic.Outline.Texture = relic.IconOutline;
            break;
        }
    }
}
