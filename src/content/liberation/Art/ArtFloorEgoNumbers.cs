namespace LibraryOfRuina.content.liberation.Art;

/// <summary>
/// 艺术层解放战 E.G.O. 页的数值。玩家拿到的卡和 Boss 打出同名 E.G.O. 的招式都读这里：
/// <c>*Damage</c> 是卡的基础伤害，也是 Boss 普通难度的伤害；<c>*UpgradedDamage</c> 既是卡升级后的伤害，
/// 也是 Boss 进阶下的伤害（用哪一级进阶由各 Boss 自己的 <c>AscensionHelper</c> 调用决定）。
/// 改这里的数会同时改卡面、卡牌效果、Boss 意图和 Boss 招式。
/// </summary>
public static class ArtFloorEgoNumbers
{
    // 彼方的碎片：ArtFloorBeyondFragmentBoss 与 BeyondFragmentEgoCard。
    public const int BeyondFragmentDamage = 8;
    public const int BeyondFragmentUpgradedDamage = 9;
    public const int BeyondFragmentHitCount = 4;
    public const int BeyondFragmentStrengthLoss = 2;
    public const int BeyondFragmentDexterityLoss = 2;

    // 欢愉：ArtFloorPleasureBoss 与 PleasureEgoCard。最终一击不随进阶变化。
    public const int PleasureDamage = 6;
    public const int PleasureUpgradedDamage = 8;
    public const int PleasureHitCount = 3;
    public const int PleasureBleedAmount = 9;
    public const int PleasureFinalDamage = 30;
    public const int PleasureStrength = 5;

    // 余香：ArtFloorNostalgicScentBoss 与 NostalgicScentEgoCard。
    public const int NostalgicScentDamage = 6;
    public const int NostalgicScentUpgradedDamage = 9;
    public const int NostalgicScentHitCount = 7;
    public const int NostalgicScentMaxHpLossOnFullBlock = 2;

    // 我们的小小银河：ArtFloorLittleGalaxyBoss 与 OurLittleGalaxyEgoPreviewCard（卡在 monsters/ArtFloorLiberation 下）。
    public const int OurLittleGalaxyDamage = 7;
    public const int OurLittleGalaxyUpgradedDamage = 8;
    public const int OurLittleGalaxyHitCount = 4;
}
