using System.Linq;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards;

internal interface IEnemyAttackPreviewCard
{
    void SetEnemyAttackPreview(IReadOnlyList<int> damages, int hits);
}

/// <summary>
/// E.G.O. 页：敌方意图用它的可变副本展示“要打出的卡”（<c>ModelDb.Card&lt;T&gt;().ToMutable()</c> 后调用
/// <see cref="UpgradePreview"/> 与 <see cref="SetEnemyAttackPreview"/>），同一个类型也是图鉴里的卡和玩家可得的卡。
/// </summary>
public abstract class EgoCardBase : CardModel, IEnemyAttackPreviewCard
{
    private string? _portraitResourcePath;

    private string PortraitResourcePath =>
        _portraitResourcePath ??= $"packed/card_portraits/ego/{ToSnakeCase(GetType().Name)}.png";

    public override string PortraitPath => ImageHelper.GetImagePath(PortraitResourcePath);

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    public override int MaxUpgradeLevel => 1;

    protected EgoCardBase(int cost)
        : base(cost, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected EgoCardBase(int cost, TargetType targetType)
        : base(cost, CardType.Attack, CardRarity.Rare, targetType)
    {
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
