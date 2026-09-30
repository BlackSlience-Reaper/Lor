namespace LibraryOfRuina.content.liberation.Technology;

/// <summary>
/// 技术层解放战 E.G.O. 页的数值。玩家拿到的卡和 Boss 打出同名 E.G.O. 的招式都读这里：
/// 卡的基础值就是 Boss 在普通难度下的值，Boss 在进阶下改用 <c>*Upgraded*</c> / <c>*Ascension*</c> 值
/// （用哪一级进阶由各 Boss 自己的 <c>AscensionHelper</c> 调用决定）。
/// 两边共用的值改一处会同时改卡面、卡牌效果、Boss 意图和 Boss 招式。
/// 魔弹八张卡与庄严哀悼不在这里：对应 Boss 的伤害、效果与卡不一致（例如贯穿卡 5 / 6、Boss 8 / 10），
/// 数值各自写在卡和 Boss 里。
/// </summary>
public static class TechnologyFloorEgoNumbers
{
    // 悔恨：TechnologyFloorRegretBoss 与 RegretEgoCard。Upgraded 是 Boss 坚韧敌人进阶的值；卡升级只减费，不读它。
    public const int RegretMultiHitDamage = 5;
    public const int RegretMultiHitUpgradedDamage = 6;
    public const int RegretMultiHitCount = 2;
    public const int RegretFinalDamage = 12;
    public const int RegretFinalUpgradedDamage = 14;
    public const int RegretConfusionAmount = 1;

    // 限制器解除：TechnologyFloorGrinderMk4Boss 与 LimiterReleaseEgoCard。Upgraded 既是卡升级后的伤害，
    // 也是 Boss 坚韧敌人进阶的伤害。
    public const int LimiterReleaseHitDamage = 6;
    public const int LimiterReleaseHitUpgradedDamage = 7;
    public const int LimiterReleaseHitCount = 3;
    public const int LimiterReleaseBleedPerHit = 1;

    // 和弦：TechnologyFloorChordBoss 与 ChordEgoCard。Ascension 是 Boss 致命敌人进阶的值；卡升级只减费，不读它。
    public const int ChordHitABaseDamage = 4;
    public const int ChordHitAAscensionDamage = 5;
    public const int ChordHitBBaseDamage = 3;
    public const int ChordHitBAscensionDamage = 4;
    public const int ChordHitCBaseDamage = 6;
    public const int ChordHitCAscensionDamage = 7;
}
