namespace LibraryOfRuina.content.liberation.History;

/// <summary>
/// 历史层解放战 E.G.O. 页的数值。玩家拿到的卡和 Boss 打出同名 E.G.O. 的招式都读这里：
/// 卡的基础值就是 Boss 在普通难度下的值，Boss 在进阶下改用 <c>*AscensionDamage</c> / <c>*HighAscensionDamage</c>
/// （用哪一级进阶由各 Boss 自己的 <c>AscensionHelper</c> 调用决定）。这几张卡升级都只减费，不读进阶值。
/// 两边共用的值改一处会同时改卡面、卡牌效果、Boss 意图和 Boss 招式。
/// </summary>
public static class HistoryFloorEgoNumbers
{
    // 惩戒一击：HistoryFloorWaspBoss 与 PunishmentStrikeEgoCard。
    public const int PunishmentStrikeBaseDamage = 20;
    public const int PunishmentStrikeAscensionDamage = 22;
    public const int PunishmentStrikeConfusionAmount = 1;
    public const int PunishmentStrikeVulnerableAmount = 2;

    // 破碎的生灵：HistoryFloorEmeraldBoughBoss 与 ShatteredLifeEgoCard（卡文件在技术层目录下）。
    public const int ShatteredLifeBaseDamage = 12;
    public const int ShatteredLifeAscensionDamage = 17;
    public const int ShatteredLifeHitCount = 3;

    // 终末之光：HistoryFloorEndLightBoss 与 EndLightEgoCard。
    public const int EndLightBaseDamage = 17;
    public const int EndLightHighAscensionDamage = 18;
    public const int EndLightBurnAmount = 7;

    // 思念的拥抱：HistoryFloorForgottenBoss 与 ForgottenLongingEmbraceEgoCard。
    public const int LongingEmbraceBaseDamage = 24;
    public const int LongingEmbraceHighAscensionDamage = 26;
    public const int LongingEmbraceConfusion = 1;

    // 饥饿狂暴：HistoryFloorFlutteringBoss 与 FlutteringHungerFrenzyEgoCard。段数、治疗、流血两边相同；
    // 多段 / 最终伤害两边原来就不同（卡 3 / 5，Boss 普通 2 / 5、致命敌人进阶 3 / 6），是否有意不详，各留各的值。
    public const int HungerFrenzyHitCount = 5;
    public const int HungerFrenzyHeal = 6;
    public const int HungerFrenzyBleed = 3;
    public const int HungerFrenzyCardMultiHitDamage = 3;
    public const int HungerFrenzyCardFinalDamage = 5;
    public const int HungerFrenzyBossMultiHitDamage = 2;
    public const int HungerFrenzyBossDeadlyMultiHitDamage = 3;
    public const int HungerFrenzyBossFinalDamage = 5;
    public const int HungerFrenzyBossDeadlyFinalDamage = 6;
}
