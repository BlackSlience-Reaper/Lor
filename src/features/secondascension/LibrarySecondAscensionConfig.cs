namespace LibraryOfRuina.features.secondascension;

internal static class LibrarySecondAscensionConfig
{
    public const int MaxLevel = 10;

    // 传闻（1级）：单人时敌人混乱抗性上限倍率。
    public const decimal RumorChaoCapMultiplierOnePlayer = 1m;

    // 传闻（1级）：双人时敌人混乱抗性上限倍率。
    public const decimal RumorChaoCapMultiplierTwoPlayers = 1.1m;

    // 传闻（1级）：三人时敌人混乱抗性上限倍率。
    public const decimal RumorChaoCapMultiplierThreePlayers = 1.3m;

    // 传闻（1级）：四人及以上时敌人混乱抗性上限倍率。
    public const decimal RumorChaoCapMultiplierFourPlayers = 1.5m;

    // 都市传说（3级）：单人时敌人生命上限倍率。
    public const decimal UrbanLegendMaxHpMultiplierOnePlayer = 1m;

    // 都市传说（3级）：双人时敌人生命上限倍率。
    public const decimal UrbanLegendMaxHpMultiplierTwoPlayers = 1.1m;

    // 都市传说（3级）：三人时敌人生命上限倍率。
    public const decimal UrbanLegendMaxHpMultiplierThreePlayers = 1.4m;

    // 都市传说（3级）：四人及以上时敌人生命上限倍率。
    public const decimal UrbanLegendMaxHpMultiplierFourPlayers = 1.6m;

    // 都市恶疾（4级）：单人时敌人每回合恢复的混乱抗性占上限比例。
    public const decimal UrbanPlagueChaoRecoveryPercentOnePlayer = 0.01m;

    // 都市恶疾（4级）：双人时敌人每回合恢复的混乱抗性占上限比例。
    public const decimal UrbanPlagueChaoRecoveryPercentTwoPlayers = 0.01m;

    // 都市恶疾（4级）：三人时敌人每回合恢复的混乱抗性占上限比例。
    public const decimal UrbanPlagueChaoRecoveryPercentThreePlayers = 0.02m;

    // 都市恶疾（4级）：四人及以上时敌人每回合恢复的混乱抗性占上限比例。
    public const decimal UrbanPlagueChaoRecoveryPercentFourPlayers = 0.03m;

    // 残响乐团（8级）：开始游戏时每名玩家失去的生命上限占当前上限比例，向下取整。
    public const decimal ReverberationEnsembleMaxHpLossPercent = 0.1m;

    // 残响乐团（8级）：开始游戏时每名玩家至少失去的生命上限。
    public const int ReverberationEnsembleMinMaxHpLoss = 1;

    public const float GoldHue = 0.13f;
    public const float GoldValue = 1.35f;
    public const string GoldOutline = "5f3f00";
}
