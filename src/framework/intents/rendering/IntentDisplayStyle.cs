namespace LibraryOfRuina.framework.intents.rendering;

/// <summary>敌方意图的画法，见模组设置“战斗界面”。存档里按名字保存，只能追加新值、不能改名。</summary>
internal enum IntentDisplayStyle
{
    /// <summary>本模组原有的画法：合成图标、徽章、详细意图文字、反击队列与敌方卡牌等。</summary>
    Default,

    /// <summary>按原版怪物的风格显示：每个效果一个原版图标，只有攻击写伤害、塞状态牌写张数，细节放在悬停提示里。</summary>
    Vanilla,
}
