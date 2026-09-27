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

    protected EgoCardBase(int cost)
        : base(cost, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected EgoCardBase(int cost, TargetType targetType)
        : base(cost, CardType.Attack, CardRarity.Rare, targetType)
    {
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
