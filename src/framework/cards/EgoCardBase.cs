using System.Linq;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.framework.cards;

internal interface IEnemyAttackPreviewCard
{
    void SetEnemyAttackPreview(IReadOnlyList<int> damages, int hits);
}

/// <summary>
/// E.G.O. 页：敌方意图用它的可变副本展示“要打出的卡”（<c>ModelDb.Card&lt;T&gt;().ToMutable()</c> 后调用
/// <see cref="UpgradePreview"/>、<see cref="SetPreviewDamage"/> 与 <see cref="SetEnemyAttackPreview"/>），
/// 同一个类型也是图鉴里的卡和玩家可得的卡。
/// </summary>
public abstract class EgoCardBase : CardModel, IEnemyAttackPreviewCard
{
    private string? _portraitResourcePath;

    // 规范 Damage 变量的初值，也是敌方预览写入的单段伤害。读它的只有 CanonicalVars 和红雾各卡的 OnPlay。
    // CanonicalVars 只在 _dynamicVars 为空时求值：规范模型首次取 DynamicVars，或 ToMutable 时规范模型还没建过
    // DynamicVars、由副本自己建的那一次（副本字段逐字段复制自规范模型）。预览只写可变副本，所以规范模型上
    // 的值始终是构造时的初值，写入后也不会回流到已经建好的 DynamicVars。
    private int _previewDamage;

    private string PortraitResourcePath =>
        _portraitResourcePath ??= $"packed/card_portraits/ego/{ToSnakeCase(GetType().Name)}.png";

    public override string PortraitPath => ImageHelper.GetImagePath(PortraitResourcePath);

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    public override int MaxUpgradeLevel => 1;

    protected int PreviewDamage => _previewDamage;

    protected EgoCardBase(int cost, int previewDamage = 0)
        : this(cost, TargetType.AnyEnemy, previewDamage)
    {
    }

    // CardModel 与 AbstractModel 的构造函数不调用虚成员，所以在这里写 _previewDamage，与原来子类字段
    // 初始化器（先于基类构造执行）对 CanonicalVars 等价。
    protected EgoCardBase(
        int cost,
        TargetType targetType,
        int previewDamage = 0,
        bool shouldShowInCardLibrary = true)
        : base(cost, CardType.Attack, CardRarity.Rare, targetType, shouldShowInCardLibrary)
    {
        _previewDamage = previewDamage;
    }

    /// <summary>
    /// 把预览卡显示为升级后的样子。直接调用原版 UpgradeInternal/FinalizeUpgradeInternal，不走
    /// CardCmd.Upgrade 的 Hook 与表现；只能用于可变副本（UpgradeInternal 会断言可变）。
    /// </summary>
    public void UpgradePreview()
    {
        UpgradeInternal();
        FinalizeUpgradeInternal();
    }

    /// <summary>
    /// 敌方意图写入单段预览伤害。没有 Damage 变量的卡只记下数值，不改 DynamicVars。
    /// </summary>
    public virtual void SetPreviewDamage(int damage)
    {
        _previewDamage = damage;
        ApplyPreviewDamage("Damage", damage);
    }

    public virtual void SetEnemyAttackPreview(IReadOnlyList<int> damages, int hits)
    {
        IReadOnlyList<int> safeDamages = damages ?? [];
        ApplyPreviewDamage("Damage", safeDamages.FirstOrDefault());

        string[] additionalDamageKeys = ["DamageB", "DamageC", "DamageD", "FinalDamage"];
        for (int i = 1; i < safeDamages.Count; i++)
        {
            foreach (string key in additionalDamageKeys.Skip(i - 1))
            {
                if (ApplyPreviewDamage(key, safeDamages[i]))
                {
                    break;
                }
            }
        }

        ApplyPreviewDamage("Hits", hits);
    }

    public static string GetPortraitResourcePath<TCard>()
        where TCard : EgoCardBase
    {
        return $"packed/card_portraits/ego/{ToSnakeCase(typeof(TCard).Name)}.png";
    }

    private static string ToSnakeCase(string pascalCase)
    {
        return Regex.Replace(pascalCase, "(?<=.)([A-Z])", "_$1").ToLowerInvariant();
    }

    private bool ApplyPreviewDamage(string key, int value)
    {
        if (!DynamicVars.TryGetValue(key, out DynamicVar? var))
        {
            return false;
        }

        var.BaseValue = value;
        return true;
    }
}
