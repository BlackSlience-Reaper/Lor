using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;

namespace LibraryOfRuina.content.reverberation.GearChurch;

public enum GearChurchMove
{
    None = -1,
    ThoughtAcceleration,
    ThoughtReveal,
    ThoughtProselytize,
    FleshEncourage,
    FleshStrengthen,
    FleshAcceleration,
    Brainwash,
    Guidance,
    DefenseInstruction,
    Assault,
    SteamEruption
}

internal static class GearChurchRules
{
    internal const int EileenMinHp = 970; // 艾琳：普通进阶体力下限。
    internal const int EileenMaxHp = 980; // 艾琳：普通进阶体力上限。
    internal const int EileenHighMinHp = 990; // 艾琳：ToughEnemies 进阶体力下限。
    internal const int EileenHighMaxHp = 1000; // 艾琳：ToughEnemies 进阶体力上限。
    internal const int EileenChao = 520; // 艾琳：单人混乱抗性上限。
    internal const int EileenInitialCapacity = 2; // 艾琳：初始意图容量。
    internal const int EileenMaximumMoves = 3; // 艾琳：情感扩容后的招式数量上限。
    internal const int FollowerMinHp = 199; // 信徒：普通进阶体力下限。
    internal const int FollowerMaxHp = 201; // 信徒：普通进阶体力上限。
    internal const int FollowerHighMinHp = 203; // 信徒：ToughEnemies 进阶体力下限。
    internal const int FollowerHighMaxHp = 205; // 信徒：ToughEnemies 进阶体力上限。
    internal const int FollowerChao = 140; // 信徒：单人混乱抗性上限。
    internal const int FollowerCapacity = 1; // 信徒：情感升级后仍固定的意图容量。
    internal const int OpeningFollowers = 2; // 开局和空场补员：生成信徒数量。
    internal const int MaximumFollowers = 3; // 新生：同时存活的信徒上限。
    internal const int FirstHpFloorPercent = 70; // 信徒的威望：第一阶段锁血百分比。
    internal const int SecondHpFloorPercent = 30; // 信徒的威望：第二阶段锁血百分比。
    internal const int FinalPhase = 3; // 艾琳：解除锁血的最终阶段。
    internal const int DeathChaoDamage = 125; // 新生：每名信徒死亡造成的单人混乱伤害。
    internal const int FabricReduction = 2; // 诺沃面料：每段攻击物理与混乱伤害减值。
    internal const int FabricProtectedHits = 3; // 诺沃面料：每回合免疫的受击段数。
    internal const int RoundSmoke = 6; // 烟气缭绕：每回合开始获得的烟气。
    internal const int SoberReductionPercent = 25; // 清醒烟气：每段物理与混乱伤害减免百分比。
    internal const int SoberSmokeCost = 1; // 清醒烟气：每段受击共同消耗的烟气层数。
    internal const int SmokeIncomingPercent = 1; // 烟气：每层承受攻击伤害的增幅百分比。
    internal const int SmokeOutgoingPercent = 2; // 烟气：每层造成伤害与混乱伤害的增幅百分比。
    internal const int SmokeBonusThreshold = 10; // 烟气：额外输出增幅的层数门槛。
    internal const int SmokeBonusPercent = 1; // 烟气：达到门槛后每层额外输出百分比。
    internal const int ThoughtAccelerationVigor = 4; // 思想齿轮-提速：全体敌人下回合开始时获得的活力层数。
    internal const int ThoughtAccelerationPlating = 9; // 思想齿轮-提速：全体敌人覆甲层数。
    internal const int ThoughtAccelerationBlock = 40; // 思想齿轮-提速：全体敌人格挡。
    internal const int RevealDamage = 9; // 思想齿轮-道破：普通进阶单次伤害。
    internal const int RevealHighDamage = 10; // 思想齿轮-道破：DeadlyEnemies 进阶单次伤害。
    internal const int RevealHits = 1; // 思想齿轮-道破：攻击次数。
    internal const int RevealVigor = 9; // 思想齿轮-道破：全体敌人下回合开始时获得的活力层数。
    internal const int RevealPlating = 6; // 思想齿轮-道破：全体敌人覆甲层数。
    internal const int ProselytizeCards = 3; // 思想齿轮-传教：普通进阶向每名玩家抽牌堆加入的晕眩张数。
    internal const int ProselytizeHighCards = 5; // 思想齿轮-传教：DeadlyEnemies 进阶向每名玩家抽牌堆加入的晕眩张数。
    internal const int ProselytizeVigor = 3; // 思想齿轮-传教：全体敌人下回合开始时获得的活力层数。
    internal const int ProselytizePlating = 8; // 思想齿轮-传教：全体敌人覆甲层数。
    internal const int EncourageHealPercent = 10; // 血肉齿轮-鼓舞：全体敌人各自最大生命的治疗百分比。
    internal const int StrengthenStrong = 2; // 血肉齿轮-强化：普通进阶永久强壮层数。
    internal const int StrengthenHighStrong = 3; // 血肉齿轮-强化：DeadlyEnemies 进阶永久强壮层数。
    internal const int StrengthenBlock = 40; // 血肉齿轮-强化：全体敌人格挡。
    internal const int FleshAccelerationBlock = 35; // 血肉齿轮-提速：艾琳自身格挡。
    internal const int FleshAccelerationVigor = 6; // 血肉齿轮-提速：全体敌人下回合开始时获得的活力层数。
    internal const int FleshAccelerationPlating = 6; // 血肉齿轮-提速：全体敌人覆甲层数。
    internal const int BrainwashDamage = 41; // 思想齿轮-洗脑：普通进阶每名玩家单次伤害。
    internal const int BrainwashHighDamage = 45; // 思想齿轮-洗脑：DeadlyEnemies 进阶每名玩家单次伤害。
    internal const int BrainwashHits = 1; // 思想齿轮-洗脑：攻击次数。
    internal const int BrainwashRinging = 1; // 思想齿轮-洗脑：原版昏眩施加量。
    internal const int GuidanceDamage = 16; // 齿轮指引：普通进阶单次伤害。
    internal const int GuidanceHighDamage = 17; // 齿轮指引：DeadlyEnemies 进阶单次伤害。
    internal const int GuidanceHits = 1; // 齿轮指引：攻击次数。
    internal const int GuidanceSmoke = 2; // 齿轮指引：整招结束后烟气层数。
    internal const int DefenseDamage = 12; // 防御指示：普通进阶单次伤害。
    internal const int DefenseHighDamage = 13; // 防御指示：DeadlyEnemies 进阶单次伤害。
    internal const int DefenseHits = 1; // 防御指示：攻击次数。
    internal const int DefenseBlock = 20; // 防御指示：信徒自身格挡。
    internal const int DefenseSmoke = 4; // 防御指示：整招结束后烟气层数。
    internal const int AssaultDamage = 4; // 自行突击：普通进阶单次伤害。
    internal const int AssaultHighDamage = 5; // 自行突击：DeadlyEnemies 进阶单次伤害。
    internal const int AssaultHits = 2; // 自行突击：攻击次数。
    internal const int AssaultSmoke = 3; // 自行突击：整招结束后烟气层数。
    internal const int SteamDamage = 25; // 蒸汽喷发：普通进阶单次伤害。
    internal const int SteamHighDamage = 27; // 蒸汽喷发：DeadlyEnemies 进阶单次伤害。
    internal const int SteamHits = 1; // 蒸汽喷发：攻击次数。
    internal const int SteamSmoke = 4; // 蒸汽喷发：整招结束后烟气层数。
    internal const float ActionSeconds = 1.5f; // 所有动作：单张姿态持续时间及伤害结算等待。

    internal static int DamageValue(int normal, int high) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, high, normal);

    internal static int HpValue(int normal, int high) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, high, normal);

    internal static int ProselytizeCardCount => DamageValue(ProselytizeCards, ProselytizeHighCards);

    internal static int Damage(GearChurchMove move) => move switch
    {
        GearChurchMove.ThoughtReveal => DamageValue(RevealDamage, RevealHighDamage),
        GearChurchMove.Brainwash => DamageValue(BrainwashDamage, BrainwashHighDamage),
        GearChurchMove.Guidance => DamageValue(GuidanceDamage, GuidanceHighDamage),
        GearChurchMove.DefenseInstruction => DamageValue(DefenseDamage, DefenseHighDamage),
        GearChurchMove.Assault => DamageValue(AssaultDamage, AssaultHighDamage),
        GearChurchMove.SteamEruption => DamageValue(SteamDamage, SteamHighDamage),
        _ => 0
    };

    internal static int Hits(GearChurchMove move) => move switch
    {
        GearChurchMove.ThoughtReveal => RevealHits,
        GearChurchMove.Brainwash => BrainwashHits,
        GearChurchMove.Guidance => GuidanceHits,
        GearChurchMove.DefenseInstruction => DefenseHits,
        GearChurchMove.Assault => AssaultHits,
        GearChurchMove.SteamEruption => SteamHits,
        _ => 0
    };

    internal static int Smoke(GearChurchMove move) => move switch
    {
        GearChurchMove.Guidance => GuidanceSmoke,
        GearChurchMove.DefenseInstruction => DefenseSmoke,
        GearChurchMove.Assault => AssaultSmoke,
        GearChurchMove.SteamEruption => SteamSmoke,
        _ => 0
    };
}
