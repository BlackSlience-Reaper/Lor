using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;

namespace LibraryOfRuina.content.liberation.Natural;

public enum NaturalFloorNihilForm
{
    Nihil,
    Greed,
    Hatred,
    Despair,
    Wrath
}

public enum NaturalFloorGirlKind
{
    Love,
    Justice,
    Happiness,
    Courage
}

public enum NaturalFloorNihilAction
{
    NihilWill,
    Hunger,
    Gluttony,
    Craving,
    Obsession,
    TyrantPath,
    Hatred,
    HatredLight,
    LoveAndHate,
    BossMagic,
    HeartPierce,
    HeartSplit,
    HeartDestroy,
    WrathGroan,
    WrathCry,
    WrathRoar,
    WrathIncarnation,
    LoveMark,
    LoveName,
    LoveHope,
    LoveMagic,
    JusticeProtect,
    JusticeDefend,
    JusticeGuard,
    JusticeHope,
    HappinessVictory,
    HappinessGlory,
    HappinessGoldenPath,
    HappinessHope,
    CourageProtect,
    CourageHelp,
    CourageJustice,
    CourageHope
}

internal sealed record NaturalFloorNihilMove(
    int NormalDamage = 0,
    int HighDamage = 0,
    int Hits = 1,
    int Block = 0,
    int Bleed = 0,
    int Corrosion = 0,
    int HatredTurns = 0,
    int NormalStrength = 0,
    int HighStrength = 0,
    int NormalStrong = 0,
    int HighStrong = 0,
    int StrongTurns = -1,
    int OtherStrong = 0,
    int HopeEndurance = 0,
    int Weak = 0,
    int WeakTurns = 0,
    int DirectHpLossPercent = 0,
    int SelfHpLoss = 0,
    bool IsGroup = false,
    bool IsTeamBlock = false,
    string Animation = "Attack",
    string Sound = "Nihil_Effect")
{
    public int Damage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, HighDamage, NormalDamage);

    public int Strength => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, HighStrength, NormalStrength);

    public int Strong => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, HighStrong, NormalStrong);
}

internal static class NaturalFloorNihilMoves
{
    // 无谓的憎恶：固定破防次数加本场玩家人数，意图判定与完整说明共用此门槛。
    internal static int HatredHitThreshold(int playerCount) =>
        HatredBaseHitThreshold + playerCount;

    internal static int DamageReduction(NaturalFloorNihilForm form) => form switch
    {
        NaturalFloorNihilForm.Greed => GreedOtherAttackerReductionPercent,
        NaturalFloorNihilForm.Hatred => HatredOtherAttackerReductionPercent,
        NaturalFloorNihilForm.Despair => DespairOtherAttackerReductionPercent,
        NaturalFloorNihilForm.Wrath => WrathOtherAttackerReductionPercent,
        _ => 0
    };

    internal static int TransitionHpLossPercent(NaturalFloorNihilForm form) => form switch
    {
        NaturalFloorNihilForm.Greed => GreedFailureHpLossPercent,
        NaturalFloorNihilForm.Hatred => HatredMagicHpLossPercent,
        NaturalFloorNihilForm.Despair => DespairSealedHpLossPercent,
        NaturalFloorNihilForm.Wrath => WrathStaggerHpLossPercent,
        _ => 0
    };

    internal const int BossHp = 999; // 虚无缥缈：所有进阶的最大体力。
    internal const int BossChaos = 300; // 虚无缥缈：混乱抗性上限。
    internal const int StatueHp = 6; // 四座石像：最大体力。
    internal const int StatueChaos = 100; // 四座石像：混乱抗性上限。
    internal const int GreedFailureHpLossPercent = 5; // 贪婪群攻全部被格挡：Boss直接损失最大生命值的百分比，按多人缩放后的最大生命值计算。
    internal const int HatredMagicHpLossPercent = 5; // 博爱成功使用魔法之力！：Boss直接损失最大生命值的百分比，按多人缩放后的最大生命值计算。
    internal const int DespairSealedHpLossPercent = 5; // 绝望三个意图全部封印：Boss直接损失最大生命值的百分比，按多人缩放后的最大生命值计算。
    internal const int WrathStaggerHpLossPercent = 5; // 愤怒陷入混乱：Boss直接损失最大生命值的百分比，按多人缩放后的最大生命值计算。
    internal const int GreedOtherAttackerReductionPercent = 50; // 贪婪：Boss受到非对应魔法少女造成的伤害与混乱伤害时的减免百分比。
    internal const int HatredOtherAttackerReductionPercent = 50; // 憎恶：Boss受到非对应魔法少女造成的伤害与混乱伤害时的减免百分比。
    internal const int DespairOtherAttackerReductionPercent = 50; // 绝望：Boss受到非对应魔法少女造成的伤害与混乱伤害时的减免百分比。
    internal const int WrathOtherAttackerReductionPercent = 50; // 愤怒：Boss受到非对应魔法少女造成的伤害与混乱伤害时的减免百分比。
    internal const int WrathGroupInterval = 2; // 邪恶的化身！！！：形态内群攻间隔回合数。
    internal const int SealedSwordCount = 3; // 无谓的绝望：需要封印的独立招式数量。
    internal const int TyrantPlayerHitThreshold = 1; // 暴君之路：触发回血所需失血玩家数量。
    internal const int NihilDebuffTurns = 1; // 虚无：每次施加三种减益的有效回合数。
    internal const int HatredVulnerableTurns = 1; // 憎恶：每次施加易损的有效回合数。
    internal const int TyrantGirlDamage = 99; // 暴君之路：对魔法少女的单次基础伤害。
    internal const int TyrantHealPercent = 5; // 暴君之路使玩家失血时：恢复最大体力的百分比。
    internal const int GreedHitBleed = 5; // 贪婪被动：每段命中附加流血层数。
    internal const int HatredBaseHitThreshold = 4; // 憎恶群攻：破防次数门槛中的固定部分，另加玩家人数。
    internal const int LoveHitThreshold = 6; // 博爱：魔法之力！插队所需的逐段破防次数。
    internal const int NihilDebuffStacks = 9; // 虚无：每回合的虚弱、破绽、易损层数。
    internal const int HatredVulnerable = 3; // 憎恶：每回合赋予的易损层数。
    internal const int ShardBlock = 99; // 第五阶段幸福的碎片：固定格挡。
    internal const int ShardCost = 0; // 第五阶段幸福的碎片：基础费用。
    internal const int ShardCount = 1; // 贪婪整次攻击未破防：给每名手中没有碎片的玩家的碎片数量。
    internal const int GreedBlockedGirlBlock = 999; // 贪婪未破防且所有玩家已有碎片：随机一名未领取奖励的少女下个玩家回合开始后获得的格挡。

    // 招式数值统一由下方具名常量控制，配置、意图、结算与动态文本共用来源。
    internal const int NihilWillNormalDamage = 19; // 虚无之意：普通进阶单次伤害。

    internal const int NihilWillHighDamage = 21; // 虚无之意：DeadlyEnemies进阶单次伤害。

    internal const int NihilWillHits = 1; // 虚无之意：攻击次数。

    internal static readonly NaturalFloorNihilMove NihilWill = new(
        NormalDamage: NihilWillNormalDamage,
        HighDamage: NihilWillHighDamage,
        Hits: NihilWillHits,
        Animation: "Special",
        Sound: "Nihil_StrongAtk"
    );

    internal const int HungerNormalDamage = 9; // 饥饿：普通进阶单次伤害。

    internal const int HungerHighDamage = 11; // 饥饿：DeadlyEnemies进阶单次伤害。

    internal const int HungerHits = 1; // 饥饿：攻击次数。

    internal const int HungerBleed = 4; // 饥饿：整招命中施加的流血层数。

    internal static readonly NaturalFloorNihilMove Hunger = new(
        NormalDamage: HungerNormalDamage,
        HighDamage: HungerHighDamage,
        Hits: HungerHits,
        Bleed: HungerBleed,
        Animation: "Slash",
        Sound: "Greed_Vert_Change"
    );

    internal const int GluttonyNormalDamage = 4; // 暴食：普通进阶单次伤害。

    internal const int GluttonyHighDamage = 5; // 暴食：DeadlyEnemies进阶单次伤害。

    internal const int GluttonyHits = 3; // 暴食：攻击次数。

    internal const int GluttonyBlock = 14; // 暴食：获得的格挡。

    internal static readonly NaturalFloorNihilMove Gluttony = new(
        NormalDamage: GluttonyNormalDamage,
        HighDamage: GluttonyHighDamage,
        Hits: GluttonyHits,
        Block: GluttonyBlock,
        Animation: "Pierce",
        Sound: "Greed_Stab_Change"
    );

    internal const int CravingNormalDamage = 5; // 渴望：普通进阶单次伤害。

    internal const int CravingHighDamage = 6; // 渴望：DeadlyEnemies进阶单次伤害。

    internal const int CravingHits = 3; // 渴望：攻击次数。

    internal static readonly NaturalFloorNihilMove Craving = new(
        NormalDamage: CravingNormalDamage,
        HighDamage: CravingHighDamage,
        Hits: CravingHits,
        Animation: "Pierce",
        Sound: "Greed_Stab_Change"
    );

    internal const int ObsessionNormalDamage = 13; // 痴迷：普通进阶单次伤害。

    internal const int ObsessionHighDamage = 14; // 痴迷：DeadlyEnemies进阶单次伤害。

    internal const int ObsessionHits = 1; // 痴迷：攻击次数。

    internal const int ObsessionNormalStrength = 1; // 痴迷：普通进阶力量层数。

    internal const int ObsessionHighStrength = 2; // 痴迷：DeadlyEnemies进阶力量层数。

    internal static readonly NaturalFloorNihilMove Obsession = new(
        NormalDamage: ObsessionNormalDamage,
        HighDamage: ObsessionHighDamage,
        Hits: ObsessionHits,
        NormalStrength: ObsessionNormalStrength,
        HighStrength: ObsessionHighStrength,
        Animation: "Slash",
        Sound: "Greed_GetPower_Change"
    );

    internal const int TyrantPathNormalDamage = 30; // 暴君之路：普通进阶单次伤害。

    internal const int TyrantPathHighDamage = 30; // 暴君之路：DeadlyEnemies进阶单次伤害。

    internal const int TyrantPathHits = 1; // 暴君之路：攻击次数。

    internal static readonly NaturalFloorNihilMove TyrantPath = new(
        NormalDamage: TyrantPathNormalDamage,
        HighDamage: TyrantPathHighDamage,
        Hits: TyrantPathHits,
        IsGroup: true,
        Animation: "Special",
        Sound: "Greed_StrongAtk_Change"
    );

    internal const int HatredNormalDamage = 5; // 憎恶：普通进阶单次伤害。

    internal const int HatredHighDamage = 6; // 憎恶：DeadlyEnemies进阶单次伤害。

    internal const int HatredHits = 3; // 憎恶：攻击次数。

    internal const int HatredHatredTurns = 3; // 憎恶：憎恶持续回合数。

    internal static readonly NaturalFloorNihilMove Hatred = new(
        NormalDamage: HatredNormalDamage,
        HighDamage: HatredHighDamage,
        Hits: HatredHits,
        HatredTurns: HatredHatredTurns,
        Animation: "Fire",
        Sound: "MagicalGirl_Gun"
    );

    internal const int HatredLightNormalDamage = 23; // 憎恶之光：普通进阶单次伤害。

    internal const int HatredLightHighDamage = 25; // 憎恶之光：DeadlyEnemies进阶单次伤害。

    internal const int HatredLightHits = 1; // 憎恶之光：攻击次数。

    internal const int HatredLightNormalStrength = 2; // 憎恶之光：普通进阶力量层数。

    internal const int HatredLightHighStrength = 3; // 憎恶之光：DeadlyEnemies进阶力量层数。

    internal static readonly NaturalFloorNihilMove HatredLight = new(
        NormalDamage: HatredLightNormalDamage,
        HighDamage: HatredLightHighDamage,
        Hits: HatredLightHits,
        NormalStrength: HatredLightNormalStrength,
        HighStrength: HatredLightHighStrength,
        Animation: "Fire",
        Sound: "MagicalGirl_Gun"
    );

    internal const int LoveAndHateNormalDamage = 7; // 以爱与恨之名：普通进阶单次伤害。

    internal const int LoveAndHateHighDamage = 8; // 以爱与恨之名：DeadlyEnemies进阶单次伤害。

    internal const int LoveAndHateHits = 4; // 以爱与恨之名：攻击次数。

    internal static readonly NaturalFloorNihilMove LoveAndHate = new(
        NormalDamage: LoveAndHateNormalDamage,
        HighDamage: LoveAndHateHighDamage,
        Hits: LoveAndHateHits,
        Animation: "Slash",
        Sound: "MagicalGirl_Atk"
    );

    internal const int BossMagicNormalDamage = 25; // 魔法之力！！！：普通进阶单次伤害。

    internal const int BossMagicHighDamage = 30; // 魔法之力！！！：DeadlyEnemies进阶单次伤害。

    internal const int BossMagicHits = 1; // 魔法之力！！！：攻击次数。

    internal static readonly NaturalFloorNihilMove BossMagic = new(
        NormalDamage: BossMagicNormalDamage,
        HighDamage: BossMagicHighDamage,
        Hits: BossMagicHits,
        IsGroup: true,
        Animation: "Special",
        Sound: "MagicalGirl_Casting"
    );

    internal const int HeartPierceNormalDamage = 9; // 穿心之剑：普通进阶单次伤害。

    internal const int HeartPierceHighDamage = 10; // 穿心之剑：DeadlyEnemies进阶单次伤害。

    internal const int HeartPierceHits = 1; // 穿心之剑：攻击次数。

    internal static readonly NaturalFloorNihilMove HeartPierce = new(
        NormalDamage: HeartPierceNormalDamage,
        HighDamage: HeartPierceHighDamage,
        Hits: HeartPierceHits,
        Animation: "Fire",
        Sound: "KnightOfDespair_Stab_gaho"
    );

    internal const int HeartSplitNormalDamage = 5; // 裂心之剑：普通进阶单次伤害。

    internal const int HeartSplitHighDamage = 6; // 裂心之剑：DeadlyEnemies进阶单次伤害。

    internal const int HeartSplitHits = 2; // 裂心之剑：攻击次数。

    internal static readonly NaturalFloorNihilMove HeartSplit = new(
        NormalDamage: HeartSplitNormalDamage,
        HighDamage: HeartSplitHighDamage,
        Hits: HeartSplitHits,
        Animation: "Fire",
        Sound: "KnightOfDespair_Hori_gaho"
    );

    internal const int HeartDestroyNormalDamage = 3; // 毁心之剑：普通进阶单次伤害。

    internal const int HeartDestroyHighDamage = 4; // 毁心之剑：DeadlyEnemies进阶单次伤害。

    internal const int HeartDestroyHits = 3; // 毁心之剑：攻击次数。

    internal static readonly NaturalFloorNihilMove HeartDestroy = new(
        NormalDamage: HeartDestroyNormalDamage,
        HighDamage: HeartDestroyHighDamage,
        Hits: HeartDestroyHits,
        Animation: "Fire",
        Sound: "KnightOfDespair_Vert_gaho"
    );

    internal const int WrathGroanNormalDamage = 15; // 呃呃呃！：普通进阶单次伤害。

    internal const int WrathGroanHighDamage = 16; // 呃呃呃！：DeadlyEnemies进阶单次伤害。

    internal const int WrathGroanHits = 2; // 呃呃呃！：攻击次数。

    internal const int WrathGroanCorrosion = 4; // 呃呃呃！：整招命中施加的下回合腐蚀层数。

    internal static readonly NaturalFloorNihilMove WrathGroan = new(
        NormalDamage: WrathGroanNormalDamage,
        HighDamage: WrathGroanHighDamage,
        Hits: WrathGroanHits,
        Corrosion: WrathGroanCorrosion,
        Animation: "Slash",
        Sound: "Angry_Hori"
    );

    internal const int WrathCryNormalDamage = 29; // 啊啊啊！：普通进阶单次伤害。

    internal const int WrathCryHighDamage = 31; // 啊啊啊！：DeadlyEnemies进阶单次伤害。

    internal const int WrathCryHits = 1; // 啊啊啊！：攻击次数。

    internal const int WrathCryCorrosion = 4; // 啊啊啊！：整招命中施加的下回合腐蚀层数。

    internal static readonly NaturalFloorNihilMove WrathCry = new(
        NormalDamage: WrathCryNormalDamage,
        HighDamage: WrathCryHighDamage,
        Hits: WrathCryHits,
        Corrosion: WrathCryCorrosion,
        Animation: "Strike",
        Sound: "Angry_Vert1"
    );

    internal const int WrathRoarNormalDamage = 10; // 啊啊啊啊！！：普通进阶单次伤害。

    internal const int WrathRoarHighDamage = 11; // 啊啊啊啊！！：DeadlyEnemies进阶单次伤害。

    internal const int WrathRoarHits = 2; // 啊啊啊啊！！：攻击次数。

    internal const int WrathRoarCorrosion = 6; // 啊啊啊啊！！：整招命中施加的下回合腐蚀层数。

    internal const int WrathRoarNormalStrong = 2; // 啊啊啊啊！！：普通进阶强壮层数。

    internal const int WrathRoarHighStrong = 3; // 啊啊啊啊！！：DeadlyEnemies进阶强壮层数。

    internal const int WrathRoarStrongTurns = -1; // 啊啊啊啊！！：强壮与希望之光的持续回合数，负值表示永久。

    internal static readonly NaturalFloorNihilMove WrathRoar = new(
        NormalDamage: WrathRoarNormalDamage,
        HighDamage: WrathRoarHighDamage,
        Hits: WrathRoarHits,
        Corrosion: WrathRoarCorrosion,
        NormalStrong: WrathRoarNormalStrong,
        HighStrong: WrathRoarHighStrong,
        StrongTurns: WrathRoarStrongTurns,
        Animation: "Slash",
        Sound: "Angry_Vert2"
    );

    internal const int WrathIncarnationNormalDamage = 7; // 邪恶的化身！！！：普通进阶单次伤害。

    internal const int WrathIncarnationHighDamage = 8; // 邪恶的化身！！！：DeadlyEnemies进阶单次伤害。

    internal const int WrathIncarnationHits = 3; // 邪恶的化身！！！：攻击次数。

    internal const int WrathIncarnationCorrosion = 5; // 邪恶的化身！！！：整招命中施加的下回合腐蚀层数。

    internal static readonly NaturalFloorNihilMove WrathIncarnation = new(
        NormalDamage: WrathIncarnationNormalDamage,
        HighDamage: WrathIncarnationHighDamage,
        Hits: WrathIncarnationHits,
        Corrosion: WrathIncarnationCorrosion,
        IsGroup: true,
        Animation: "Special",
        Sound: "Angry_StrongAtk1"
    );

    internal const int LoveMarkNormalDamage = 21; // 魔法标记：普通进阶单次伤害。

    internal const int LoveMarkHighDamage = 21; // 魔法标记：DeadlyEnemies进阶单次伤害。

    internal const int LoveMarkHits = 1; // 魔法标记：攻击次数。

    internal const int LoveMarkHatredTurns = 2; // 魔法标记：憎恶持续回合数。

    internal static readonly NaturalFloorNihilMove LoveMark = new(
        NormalDamage: LoveMarkNormalDamage,
        HighDamage: LoveMarkHighDamage,
        Hits: LoveMarkHits,
        HatredTurns: LoveMarkHatredTurns,
        Animation: "Fire",
        Sound: "MagicalGirl_Gun"
    );

    internal const int LoveNameNormalDamage = 10; // 以爱之名！：普通进阶单次伤害。

    internal const int LoveNameHighDamage = 10; // 以爱之名！：DeadlyEnemies进阶单次伤害。

    internal const int LoveNameHits = 2; // 以爱之名！：攻击次数。

    internal const int LoveNameBlock = 11; // 以爱之名！：获得的格挡。

    internal static readonly NaturalFloorNihilMove LoveName = new(
        NormalDamage: LoveNameNormalDamage,
        HighDamage: LoveNameHighDamage,
        Hits: LoveNameHits,
        Block: LoveNameBlock,
        Sound: "MagicalGirl_Atk"
    );

    internal const int LoveHopeOtherStrong = 3; // 希望之光：其他魔法少女获得的强壮层数。

    internal const int LoveHopeHopeEndurance = 3; // 希望之光：其他魔法少女获得的忍耐层数。

    internal const int LoveHopeStrongTurns = 1; // 希望之光：强壮与希望之光的持续回合数，负值表示永久。

    internal static readonly NaturalFloorNihilMove LoveHope = new(
        OtherStrong: LoveHopeOtherStrong,
        HopeEndurance: LoveHopeHopeEndurance,
        StrongTurns: LoveHopeStrongTurns,
        Animation: "Cast",
        Sound: "MagicalGirl_CastEnd"
    );

    internal const int LoveMagicNormalDamage = 15; // 魔法之力！：普通进阶单次伤害。

    internal const int LoveMagicHighDamage = 15; // 魔法之力！：DeadlyEnemies进阶单次伤害。

    internal const int LoveMagicHits = 1; // 魔法之力！：攻击次数。

    internal static readonly NaturalFloorNihilMove LoveMagic = new(
        NormalDamage: LoveMagicNormalDamage,
        HighDamage: LoveMagicHighDamage,
        Hits: LoveMagicHits,
        Animation: "Special",
        Sound: "MagicalGirl_Casting"
    );

    internal const int JusticeProtectNormalDamage = 12; // 护心之剑：普通进阶单次伤害。

    internal const int JusticeProtectHighDamage = 12; // 护心之剑：DeadlyEnemies进阶单次伤害。

    internal const int JusticeProtectHits = 1; // 护心之剑：攻击次数。

    internal const int JusticeProtectDirectHpLossPercent = 3; // 护心之剑：整招破防后追加损失目标最大生命值的百分比，上限一次。

    internal static readonly NaturalFloorNihilMove JusticeProtect = new(
        NormalDamage: JusticeProtectNormalDamage,
        HighDamage: JusticeProtectHighDamage,
        Hits: JusticeProtectHits,
        DirectHpLossPercent: JusticeProtectDirectHpLossPercent,
        Animation: "Pierce",
        Sound: "KnightOfDespair_Stab"
    );

    internal const int JusticeDefendNormalDamage = 9; // 卫心之剑：普通进阶单次伤害。

    internal const int JusticeDefendHighDamage = 9; // 卫心之剑：DeadlyEnemies进阶单次伤害。

    internal const int JusticeDefendHits = 2; // 卫心之剑：攻击次数。

    internal const int JusticeDefendDirectHpLossPercent = 5; // 卫心之剑：整招破防后追加损失目标最大生命值的百分比，上限一次。

    internal static readonly NaturalFloorNihilMove JusticeDefend = new(
        NormalDamage: JusticeDefendNormalDamage,
        HighDamage: JusticeDefendHighDamage,
        Hits: JusticeDefendHits,
        DirectHpLossPercent: JusticeDefendDirectHpLossPercent,
        Animation: "Slash",
        Sound: "KnightOfDespair_Hori"
    );

    internal const int JusticeGuardNormalDamage = 7; // 御心之剑：普通进阶单次伤害。

    internal const int JusticeGuardHighDamage = 7 ; // 御心之剑：DeadlyEnemies进阶单次伤害。

    internal const int JusticeGuardHits = 3; // 御心之剑：攻击次数。

    internal const int JusticeGuardDirectHpLossPercent = 7; // 御心之剑：整招破防后追加损失目标最大生命值的百分比，上限一次。

    internal static readonly NaturalFloorNihilMove JusticeGuard = new(
        NormalDamage: JusticeGuardNormalDamage,
        HighDamage: JusticeGuardHighDamage,
        Hits: JusticeGuardHits,
        DirectHpLossPercent: JusticeGuardDirectHpLossPercent,
        Animation: "Strike",
        Sound: "KnightOfDespair_Vert"
    );

    internal const int JusticeHopeOtherStrong = 3; // 希望之光：其他魔法少女获得的强壮层数。

    internal const int JusticeHopeHopeEndurance = 3; // 希望之光：其他魔法少女获得的忍耐层数。

    internal const int JusticeHopeStrongTurns = 1; // 希望之光：强壮与希望之光的持续回合数，负值表示永久。

    internal static readonly NaturalFloorNihilMove JusticeHope = new(
        OtherStrong: JusticeHopeOtherStrong,
        HopeEndurance: JusticeHopeHopeEndurance,
        StrongTurns: JusticeHopeStrongTurns,
        Animation: "Cast",
        Sound: "KnightOfDespair_Gaho"
    );

    internal const int HappinessVictoryBlock = 10; // 胜利的陶醉：获得的格挡。

    internal const int HappinessVictoryNormalStrong = 5; // 胜利的陶醉：普通进阶强壮层数。

    internal const int HappinessVictoryHighStrong = 4; // 胜利的陶醉：DeadlyEnemies进阶强壮层数。

    internal const int HappinessVictoryOtherStrong = 2; // 胜利的陶醉：其他魔法少女获得的强壮层数。

    internal const int HappinessVictoryStrongTurns = -1; // 胜利的陶醉：强壮与希望之光的持续回合数，负值表示永久。

    internal static readonly NaturalFloorNihilMove HappinessVictory = new(
        Block: HappinessVictoryBlock,
        IsTeamBlock: true,
        NormalStrong: HappinessVictoryNormalStrong,
        HighStrong: HappinessVictoryHighStrong,
        OtherStrong: HappinessVictoryOtherStrong,
        StrongTurns: HappinessVictoryStrongTurns,
        Animation: "Cast",
        Sound: "Greed_GetPower"
    );

    internal const int HappinessGloryBlock = 10; // 压倒的光彩：获得的格挡。

    internal const int HappinessGloryWeak = 3; // 压倒的光彩：对敌方施加的虚弱层数。

    internal const int HappinessGloryWeakTurns = 1; // 压倒的光彩：虚弱持续回合数。

    internal static readonly NaturalFloorNihilMove HappinessGlory = new(
        Block: HappinessGloryBlock,
        IsTeamBlock: true,
        Weak: HappinessGloryWeak,
        WeakTurns: HappinessGloryWeakTurns,
        Animation: "Cast",
        Sound: "Greed_GetPower"
    );

    internal const int HappinessGoldenPathNormalDamage = 25; // 闪金之路：普通进阶单次伤害。

    internal const int HappinessGoldenPathHighDamage = 25; // 闪金之路：DeadlyEnemies进阶单次伤害。

    internal const int HappinessGoldenPathHits = 1; // 闪金之路：攻击次数。

    internal static readonly NaturalFloorNihilMove HappinessGoldenPath = new(
        NormalDamage: HappinessGoldenPathNormalDamage,
        HighDamage: HappinessGoldenPathHighDamage,
        Hits: HappinessGoldenPathHits,
        Animation: "Special",
        Sound: "Greed_StrongAtk"
    );

    internal const int HappinessHopeOtherStrong = 3; // 希望之光：其他魔法少女获得的强壮层数。

    internal const int HappinessHopeHopeEndurance = 3; // 希望之光：其他魔法少女获得的忍耐层数。

    internal const int HappinessHopeStrongTurns = 1; // 希望之光：强壮与希望之光的持续回合数，负值表示永久。

    internal static readonly NaturalFloorNihilMove HappinessHope = new(
        OtherStrong: HappinessHopeOtherStrong,
        HopeEndurance: HappinessHopeHopeEndurance,
        StrongTurns: HappinessHopeStrongTurns,
        Animation: "Cast",
        Sound: "Greed_GetPower"
    );

    internal const int CourageProtectNormalDamage = 9; // 朋友由我来守护：普通进阶单次伤害。

    internal const int CourageProtectHighDamage = 9; // 朋友由我来守护：DeadlyEnemies进阶单次伤害。

    internal const int CourageProtectHits = 2; // 朋友由我来守护：攻击次数。

    internal const int CourageProtectCorrosion = 2; // 朋友由我来守护：整招命中施加的下回合腐蚀层数。

    internal static readonly NaturalFloorNihilMove CourageProtect = new(
        NormalDamage: CourageProtectNormalDamage,
        HighDamage: CourageProtectHighDamage,
        Hits: CourageProtectHits,
        Corrosion: CourageProtectCorrosion,
        Animation: "Slash",
        Sound: "Angry_R_Atk"
    );

    internal const int CourageHelpNormalDamage = 17; // 只要能帮上忙！：普通进阶单次伤害。

    internal const int CourageHelpHighDamage = 17; // 只要能帮上忙！：DeadlyEnemies进阶单次伤害。

    internal const int CourageHelpHits = 1; // 只要能帮上忙！：攻击次数。

    internal const int CourageHelpNormalStrong = 4; // 只要能帮上忙！：普通进阶强壮层数。

    internal const int CourageHelpHighStrong = 4; // 只要能帮上忙！：DeadlyEnemies进阶强壮层数。

    internal const int CourageHelpStrongTurns = 1; // 只要能帮上忙！：强壮与希望之光的持续回合数，负值表示永久。

    internal const int CourageHelpSelfHpLoss = 5; // 只要能帮上忙！：自身直接失去的生命。

    internal static readonly NaturalFloorNihilMove CourageHelp = new(
        NormalDamage: CourageHelpNormalDamage,
        HighDamage: CourageHelpHighDamage,
        Hits: CourageHelpHits,
        NormalStrong: CourageHelpNormalStrong,
        HighStrong: CourageHelpHighStrong,
        StrongTurns: CourageHelpStrongTurns,
        SelfHpLoss: CourageHelpSelfHpLoss,
        Animation: "Strike",
        Sound: "Angry_R_WandHit"
    );

    internal const int CourageJusticeNormalDamage = 8; // 为了此地的正义与均衡！：普通进阶单次伤害。

    internal const int CourageJusticeHighDamage = 8; // 为了此地的正义与均衡！：DeadlyEnemies进阶单次伤害。

    internal const int CourageJusticeHits = 3; // 为了此地的正义与均衡！：攻击次数。

    internal const int CourageJusticeCorrosion = 5; // 为了此地的正义与均衡！：整招命中施加的下回合腐蚀层数。

    internal static readonly NaturalFloorNihilMove CourageJustice = new(
        NormalDamage: CourageJusticeNormalDamage,
        HighDamage: CourageJusticeHighDamage,
        Hits: CourageJusticeHits,
        Corrosion: CourageJusticeCorrosion,
        Animation: "Special",
        Sound: "Angry_R_StrongAtk"
    );

    internal const int CourageHopeOtherStrong = 3; // 希望之光：其他魔法少女获得的强壮层数。

    internal const int CourageHopeHopeEndurance = 3; // 希望之光：其他魔法少女获得的忍耐层数。

    internal const int CourageHopeStrongTurns = 1; // 希望之光：强壮与希望之光的持续回合数，负值表示永久。

    internal static readonly NaturalFloorNihilMove CourageHope = new(
        OtherStrong: CourageHopeOtherStrong,
        HopeEndurance: CourageHopeHopeEndurance,
        StrongTurns: CourageHopeStrongTurns,
        Animation: "Cast",
        Sound: "Angry_Meet"
    );

    internal static NaturalFloorNihilMove Get(NaturalFloorNihilAction action) => action switch
    {
        NaturalFloorNihilAction.NihilWill => NihilWill,
        NaturalFloorNihilAction.Hunger => Hunger,
        NaturalFloorNihilAction.Gluttony => Gluttony,
        NaturalFloorNihilAction.Craving => Craving,
        NaturalFloorNihilAction.Obsession => Obsession,
        NaturalFloorNihilAction.TyrantPath => TyrantPath,
        NaturalFloorNihilAction.Hatred => Hatred,
        NaturalFloorNihilAction.HatredLight => HatredLight,
        NaturalFloorNihilAction.LoveAndHate => LoveAndHate,
        NaturalFloorNihilAction.BossMagic => BossMagic,
        NaturalFloorNihilAction.HeartPierce => HeartPierce,
        NaturalFloorNihilAction.HeartSplit => HeartSplit,
        NaturalFloorNihilAction.HeartDestroy => HeartDestroy,
        NaturalFloorNihilAction.WrathGroan => WrathGroan,
        NaturalFloorNihilAction.WrathCry => WrathCry,
        NaturalFloorNihilAction.WrathRoar => WrathRoar,
        NaturalFloorNihilAction.WrathIncarnation => WrathIncarnation,
        NaturalFloorNihilAction.LoveMark => LoveMark,
        NaturalFloorNihilAction.LoveName => LoveName,
        NaturalFloorNihilAction.LoveHope => LoveHope,
        NaturalFloorNihilAction.LoveMagic => LoveMagic,
        NaturalFloorNihilAction.JusticeProtect => JusticeProtect,
        NaturalFloorNihilAction.JusticeDefend => JusticeDefend,
        NaturalFloorNihilAction.JusticeGuard => JusticeGuard,
        NaturalFloorNihilAction.JusticeHope => JusticeHope,
        NaturalFloorNihilAction.HappinessVictory => HappinessVictory,
        NaturalFloorNihilAction.HappinessGlory => HappinessGlory,
        NaturalFloorNihilAction.HappinessGoldenPath => HappinessGoldenPath,
        NaturalFloorNihilAction.HappinessHope => HappinessHope,
        NaturalFloorNihilAction.CourageProtect => CourageProtect,
        NaturalFloorNihilAction.CourageHelp => CourageHelp,
        NaturalFloorNihilAction.CourageJustice => CourageJustice,
        NaturalFloorNihilAction.CourageHope => CourageHope,
        _ => throw new System.ArgumentOutOfRangeException(nameof(action))
    };
}
