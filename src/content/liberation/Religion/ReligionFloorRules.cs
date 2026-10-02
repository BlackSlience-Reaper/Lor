using System;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;

namespace LibraryOfRuina.content.liberation.Religion;

internal static class ReligionFloorRules
{
    // 第二阶段的救赎判定、生命下限和血条白色区域共用同一取整阈值。
    internal static int SecondPhaseMinimumHp(int maximumHp) =>
        (int)Math.Ceiling(maximumHp * SecondPhaseMinimumPercent / 100m);

    internal const int ParadiseHp = 666; // 失乐园：基础最大体力。
    internal const int ParadiseChao = 666; // 失乐园：最大混乱抗性。
    internal const int FirstPhaseMinimumHp = 1; // 第一阶段：生命下限。
    internal const int SecondPhaseMinimumPercent = 50; // 第二阶段：成功阈值及生命下限百分比。
    internal const int DamageReductionPercent = 75; // 免疫：所有来源生命伤害减免百分比。
    internal const int ApostleFakeDeathHp = 0; // 不死：进入原版死亡保留状态时的生命值。
    internal const int FalseDeathThreshold = 2; // 不死：进入假死的体力阈值。
    internal const int RequiredKills = 12; // 时机已然成熟：胜利所需新增假死次数。
    internal const int SurvivalHp = 1; // 十二使徒结局：存活玩家保留体力。
    internal const int TrialTurns = 3; // 为何不安：第二阶段完整敌方回合数。
    internal const int ExplosionMultiplier = 2; // 自爆：队伍最高最大生命的伤害倍率。
    internal const int CrownProtection = 999; // 荆棘之冠：永久守护层数。
    internal const float RepentanceSeconds = 6f; // 赎罪：忏悔姿态停留秒数。
    internal const float FrameSeconds = 1f; // 敌人场景：每个动作帧持续秒数。
    internal const int AweFirstTurn = 2; // 起身迎接我吧：首次施放的敌方回合。
    internal const int AweInterval = 3; // 起身迎接我吧：施放间隔。
    internal const int AweCards = 6; // 敬畏：每名玩家受影响的出牌次数及翼节点数。
    internal const int SacrificeHealPercent = 6; // 献祭：每张成功消耗牌恢复的最大生命百分比。
    internal const int SoloReclaimPercent = 12; // 敬畏夺回：单人失血阈值百分比。
    internal const int DuoReclaimPercent = 15; // 敬畏夺回：双人失血阈值百分比。
    internal const int TrioReclaimPercent = 20; // 敬畏夺回：三人失血阈值百分比。
    internal const int QuartetReclaimPercent = 30; // 敬畏夺回：四人失血阈值百分比。
    internal const int OneDeadReviveChance = 30; // 救主：一名假死使徒时的唤醒概率。
    internal const int TwoDeadReviveChance = 60; // 救主：两名假死使徒时的唤醒概率。
    internal const int ThreeDeadReviveChance = 90; // 救主：三名假死使徒时的唤醒概率。
    internal const int WelcomeBlockNormal = 55; // 不要惊惶：普通进阶给予存活敌人的格挡。
    internal const int WelcomeBlockHigh = 66; // 不要惊惶：DeadlyEnemies 进阶给予的格挡。
    internal const int WelcomeStrengthNormal = 1; // 不要惊惶：普通进阶力量。
    internal const int WelcomeStrengthHigh = 2; // 不要惊惶：DeadlyEnemies 进阶力量。
    internal const int SaviorDamageMin = 14; // 我是你的救主：单次伤害下限。
    internal const int SaviorDamageMax = 16; // 我是你的救主：单次伤害上限。
    internal const int SaviorHits = 1; // 我是你的救主：攻击次数。
    internal const int SaviorWakeCount = 1; // 我是你的救主：场上仍有其他未假死敌人时唤醒的使徒数。
    internal const int SaviorLoneWakeCount = 2; // 我是你的救主：场上没有其他未假死敌人时唤醒的使徒数。

    internal static int WelcomeBlock => DamageValue(WelcomeBlockNormal, WelcomeBlockHigh);

    internal static int WelcomeStrength => DamageValue(WelcomeStrengthNormal, WelcomeStrengthHigh);

    internal static int DamageValue(int normal, int high) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, high, normal);

    internal static int HpValue(int normal, int high) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, high, normal);

    internal static int ReclaimPercent(int playerCount) => playerCount switch
    {
        1 => SoloReclaimPercent,
        2 => DuoReclaimPercent,
        3 => TrioReclaimPercent,
        _ => QuartetReclaimPercent
    };
}
