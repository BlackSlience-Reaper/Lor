using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;

namespace LibraryOfRuina.reverberation.CryingChildren;

public enum CryingMove
{
    None = -1,
    DespairBrand,
    ColdSun,
    EmotionalTurbulence,
    BurningCourage,
    SelfRestraint,
    SearingPain,
    FierceMomentum,
    BlazingWill,
    ScorchedAsh,
    Murmur,
    FoulWings,
    EndlessTorment
}

internal static class CryingChildrenRules
{
    internal const int PhilipMinHp = 950; // 菲利普：普通进阶体力下限。
    internal const int PhilipMaxHp = 990; // 菲利普：普通进阶体力上限。
    internal const int PhilipHighMinHp = 1010; // 菲利普：ToughEnemies 进阶体力下限。
    internal const int PhilipHighMaxHp = 1050; // 菲利普：ToughEnemies 进阶体力上限。
    internal const int PhilipChao = 380; // 菲利普：单人混乱抗性上限。
    internal const int ChildMinHp = 150; // 不言之子：普通进阶体力下限。
    internal const int ChildMaxHp = 160; // 不言之子：普通进阶体力上限。
    internal const int ChildHighMinHp = 170; // 不言之子：ToughEnemies 进阶体力下限。
    internal const int ChildHighMaxHp = 180; // 不言之子：ToughEnemies 进阶体力上限。
    internal const int ChildChao = 120; // 不言之子：单人混乱抗性上限。
    internal const int ChildCount = 3; // 第二阶段：只召唤一次三个不言之子。
    internal const int InitialIntentCount = 2; // 菲利普：初始每行至多执行两招。
    internal const int ExpandedIntentCount = 3; // 菲利普：情感扩容后至多执行三招。
    internal const int PatternRoundCount = 3; // 菲利普：各阶段行动循环的回合数。
    internal const int ChildIntentCount = 1; // 不言之子：每回合执行一招。
    internal const int FirstHpFloorPercent = 60; // 心潮澎湃：第一阶段体力下限百分比。
    internal const int SecondHpFloorPercent = 30; // 涌动之心：第二阶段体力下限百分比。
    internal const int PhilipHeatThreshold = 30; // 菲利普过热：回合开始所需烧伤层数。
    internal const int ChildHeatThreshold = 15; // 不言之子过热：回合开始所需烧伤层数。
    internal const int HeatStrength = 3; // 过热：每次进入时获得的永久力量。
    internal const int ChildHeatVulnerable = 3; // 不言之子过热：每次进入时获得的永久易损。
    internal const int PhilipHeatBurn = 6; // 菲利普过热：每次击中或受击施加的烧伤。
    internal const int ChildHeatBurn = 4; // 不言之子过热：每次击中施加的烧伤。
    internal const int BladeBurn = 12; // 爆炎之刃：每次击中或受击施加的烧伤。
    internal const int FabricReduction = 3; // 诺沃面料：普通进阶每次攻击的伤害与混乱伤害减值。
    internal const int FabricHighReduction = 5; // 诺沃面料：DeadlyEnemies 进阶每次攻击的伤害与混乱伤害减值。
    internal const decimal ChildBurnMultiplier = 0.5m; // 火热之心：烧伤伤害倍率。
    internal const int ChildGrowthStrong = 1; // 火热之心：生成后每回合永久强壮层数。
    internal const int ChildGrowthEndurance = 1; // 火热之心：生成后每回合永久忍耐层数。
    internal const int SwiftStrong = 3; // 迅猛：生效回合获得的强壮层数。
    internal const int SwiftTurns = 1; // 迅猛：强壮和烧伤增量的持续回合。
    internal const int SwiftBurnBonus = 3; // 迅猛：每次正数烧伤施加的额外层数。
    internal const int MomentumSwift = 1; // 迅猛气势：获得的迅猛层数。
    internal const int ColdSunBlock = 45; // 冰冷残阳：获得格挡。
    internal const int TurbulenceDamage = 33; // 情感碰撞：普通进阶单次伤害。
    internal const int TurbulenceHighDamage = 35; // 情感碰撞：DeadlyEnemies 进阶单次伤害。
    internal const int TurbulenceHits = 1; // 情感碰撞：攻击次数。
    internal const int BrandDamage = 12; // 绝望烙印：普通进阶单次伤害。
    internal const int BrandHighDamage = 15; // 绝望烙印：DeadlyEnemies 进阶单次伤害。
    internal const int BrandHits = 2; // 绝望烙印：攻击次数。
    internal const int BrandEmotion = 3; // 绝望烙印：整招获得情感点数。
    internal const int CourageCards = 4; // 灼烧勇气：普通进阶每名玩家手牌灼烧数量。
    internal const int CourageHighCards = 5; // 灼烧勇气：DeadlyEnemies 进阶每名玩家手牌灼烧数量。
    internal const int RestraintDamage = 27; // 自我束缚：普通进阶单次伤害。
    internal const int RestraintHighDamage = 29; // 自我束缚：DeadlyEnemies 进阶单次伤害。
    internal const int RestraintHits = 1; // 自我束缚：攻击次数。
    internal const int RestraintWeak = 5; // 自我束缚：目标永久虚弱层数。
    internal const int RestraintBinding = 5; // 自我束缚：目标永久束缚层数。
    internal const int RestraintDisarm = 5; // 自我束缚：目标永久破绽层数。
    internal const int PainStacks = 2; // 痛彻骨髓：全体玩家贯通创伤层数。
    internal const int MomentumBlock = 80; // 迅猛气势：获得格挡。
    internal const int WillDamage = 4; // 爆燃意志：普通进阶单次伤害。
    internal const int WillHighDamage = 5; // 爆燃意志：DeadlyEnemies 进阶单次伤害。
    internal const int WillBurnPerPlayer = 6; // 爆燃意志：每名玩家对应一次攻击所需的目标方总烧伤。
    internal const int WillMinimumHits = 3; // 爆燃意志：向下取整后的最低攻击次数。
    internal const int AshDamage = 45; // 焦灼死灰：普通进阶每名目标单次伤害。
    internal const int AshHighDamage = 50; // 焦灼死灰：DeadlyEnemies 进阶每名目标单次伤害。
    internal const int AshHits = 1; // 焦灼死灰：对每名目标的攻击次数。
    internal const int AshBurn = 20; // 焦灼死灰：向每名玩家施加的烧伤。
    internal const int MurmurDamage = 12; // 喁喁细语：普通进阶单次伤害。
    internal const int MurmurHighDamage = 14; // 喁喁细语：DeadlyEnemies 进阶单次伤害。
    internal const int MurmurHits = 1; // 喁喁细语：攻击次数。
    internal const int MurmurBlock = 20; // 喁喁细语：获得格挡。
    internal const int WingsStrengthLoss = 2; // 秽翼抽打：全体玩家永久失去的力量。
    internal const int WingsDexterityLoss = 2; // 秽翼抽打：全体玩家永久失去的敏捷。
    internal const int TormentCards = 2; // 无尽折磨：普通进阶每名玩家弃牌堆晕眩数量。
    internal const int TormentHighCards = 3; // 无尽折磨：DeadlyEnemies 进阶每名玩家弃牌堆晕眩数量。
    internal const float AnimationFrameSeconds = 1.5f; // 哭泣之子与不言之子：每个动画帧默认持续一秒。
    internal const float ActionSeconds = AnimationFrameSeconds; // 单帧原地动作总长，与内嵌动画库一致。
    internal const float ImpactSeconds = AnimationFrameSeconds; // 单帧攻击保持一秒后结算伤害。

    internal static int DamageValue(int normal, int high) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, high, normal);

    internal static int HpValue(int normal, int high) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, high, normal);

    internal static int Damage(CryingMove move) => move switch
    {
        CryingMove.DespairBrand => DamageValue(BrandDamage, BrandHighDamage),
        CryingMove.EmotionalTurbulence => DamageValue(TurbulenceDamage, TurbulenceHighDamage),
        CryingMove.SelfRestraint => DamageValue(RestraintDamage, RestraintHighDamage),
        CryingMove.BlazingWill => DamageValue(WillDamage, WillHighDamage),
        CryingMove.ScorchedAsh => DamageValue(AshDamage, AshHighDamage),
        CryingMove.Murmur => DamageValue(MurmurDamage, MurmurHighDamage),
        _ => 0
    };

    internal static IReadOnlyList<CryingMove> Pattern(int phase, int row) => (phase, row) switch
    {
        (1, 0) => [CryingMove.DespairBrand, CryingMove.ColdSun],
        (1, 1) => [CryingMove.ColdSun, CryingMove.DespairBrand, CryingMove.EmotionalTurbulence],
        (1, _) => [CryingMove.BurningCourage, CryingMove.DespairBrand, CryingMove.EmotionalTurbulence],
        (2, 0) => [CryingMove.SelfRestraint, CryingMove.SearingPain, CryingMove.EmotionalTurbulence],
        (2, 1) => [CryingMove.SearingPain, CryingMove.FierceMomentum],
        (2, _) => [CryingMove.BlazingWill, CryingMove.BurningCourage],
        (_, 0) => [CryingMove.ScorchedAsh, CryingMove.FierceMomentum],
        (_, 1) => [CryingMove.BurningCourage, CryingMove.EmotionalTurbulence, CryingMove.BurningCourage],
        _ => [CryingMove.BlazingWill, CryingMove.ColdSun, CryingMove.EmotionalTurbulence]
    };
}
