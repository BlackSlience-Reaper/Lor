using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

/// <summary>
/// 愤怒侍从 — NPC协防雇佣兵。
/// 优先攻击隐士之杖，无杖时攻击青林隐士。
/// 招式:
///   Move1 "呃呃呃！" — (9-10)×2
///   Move2 "啊啊啊！" — (8-9)×2 + 下回合腐蚀 + 易伤 (仅杖存在时)
///   Move3 "啊啊啊啊！" — (7-8)×3 + 下回合腐蚀 + 自身+2力量
///   Move4 "邪恶的化身！！！" — AoE 3段 (4-5+2下回合腐蚀, 6-7+2下回合腐蚀, 9-10+6下回合腐蚀)
/// </summary>
public sealed class WrathServant : LorMonsterModel, ITargetedMonsterAttackProvider
{
    private const string UuughMoveId = "UUUGH";
    private const string AaahMoveId = "AAAH";
    private const string AaaahMoveId = "AAAAH";
    private const string EvilIncarnationMoveId = "EVIL_INCARNATION";
    private const string MainBranchId = "MAIN_BRANCH";
    private const string NormalRandomId = "NORMAL_RAND";
    private const string SpecialCheckId = "SPECIAL_CHECK";

    private const int UuughHits = 2;
    private const int AaahHits = 2;
    private const int AaaahHits = 3;
    private const int AaahCorrosionPerHit = 1;
    private const int AaahVulnerable = 2;
    private const int AaaahCorrosionPerHit = 1;
    private const int AaaahStrength = 2;
    private const int EvilCorrosion1 = 1;
    private const int EvilCorrosion2 = 1;
    private const int EvilCorrosion3 = 3;
    private const float SegmentDelaySeconds = 0.48f;
    private const float SpecialSegmentDelaySeconds = 0.6f;

    private const string Root = "res://images/monsters/wrath_servant/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string AttackStrikeTexturePath = Root + "attack_strike.png";
    public const string AttackSlashTexturePath = Root + "attack_slash.png";
    public const string AttackSlash2TexturePath = Root + "attack_slash2.png";
    public const string S1TexturePath = Root + "s1.png";
    public const string S2TexturePath = Root + "s2.png";
    public const string S3TexturePath = Root + "s3.png";
    public const string SpecialTexturePath = Root + "special.png";

    private static readonly string[] SfxPaths =
    [
        WrathServantEncounterHelper.ServantSfxRoot + "attack_strike.ogg",
        WrathServantEncounterHelper.ServantSfxRoot + "attack_thrust.ogg",
        WrathServantEncounterHelper.ServantSfxRoot + "attack_slash.ogg",
        WrathServantEncounterHelper.ServantSfxRoot + "special_1.ogg",
        WrathServantEncounterHelper.ServantSfxRoot + "special_2.ogg",
        WrathServantEncounterHelper.ServantSfxRoot + "special_end.ogg"
    ];

    public static readonly IReadOnlyList<string> AssetPathsStatic =
        WrathServantCreatureVisuals.AssetPaths
            .Concat(SfxPaths)
            .ToArray();

    private bool _pendingSpecialAttack;
    private int _normalAttackSfxCursor;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 266, 300);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 270, 304);

    public override int DefaultChaoResistance => 120;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Fatal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Fatal,
        Blunt = LibraryResistanceLevel.Normal
    };

    private int UuughDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 17, 19);

    private int AaahDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 11);

    private int AaaahDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 10);

    private int EvilDamage1 =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 1);

    private int EvilDamage2 =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 3);

    private int EvilDamage3 =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 5);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>();
            paths.AddRange(
                WrathServantCreatureVisuals.AssetPaths);
            paths.AddRange(SfxPaths);

            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    /// <summary>由 SinnerCounterPower 调用，标记下一回合使用 Move4</summary>
    public void MarkPendingSpecialAttack()
    {
        _pendingSpecialAttack = true;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _pendingSpecialAttack = false;
        _normalAttackSfxCursor = 0;

        EncounterBgmController.RegisterMonster(Creature);

        // 应用异界的罪人累计伤害追踪器
        await PowerCmdCompat.Apply<WrathServantTodayPlayPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<WrathServantSinnerCounterPower>(Creature, WrathServantSinnerCounterPower.DamageThreshold, Creature, null, silent: true);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        await base.AfterDamageReceived(choiceContext, target, result, props, dealer, cardSource);

        // 记录死亡归因（供 TodayPlayPower 在 AfterDeath 中读取）
        if (target == Creature && Creature.IsDead)
        {
            WrathServantDeathContext.Record(Creature, dealer);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // Move states
        var uuugh = new MoveState(
            UuughMoveId,
            UuughMove,
            new TargetedMonsterAttackIntent(
                () => UuughDamage,
                () => UuughHits,
                "WRATH_SERVANT_UUUGH.description"));

        var aaah = new MoveState(
            AaahMoveId,
            AaahMove,
            new TargetedMonsterAttackIntent(
                () => AaahDamage,
                () => AaahHits,
                "WRATH_SERVANT_AAAH.description"),
            new DebuffIntent());

        var aaaah = new MoveState(
            AaaahMoveId,
            AaaahMove,
            new TargetedMonsterAttackIntent(
                () => AaaahDamage,
                () => AaaahHits,
                "WRATH_SERVANT_AAAAH.description"),
            new BuffIntent());

        var evilIncarnation = new MoveState(
            EvilIncarnationMoveId,
            EvilIncarnationMove,
            new IndiscriminateAttackIntent(
                () => EvilDamage1,
                () => 1,
                "WRATH_SERVANT_EVIL_INCARNATION.description"),
            new IndiscriminateAttackIntent(
                () => EvilDamage2,
                () => 1,
                "WRATH_SERVANT_EVIL_INCARNATION.description"),
            new IndiscriminateAttackIntent(
                () => EvilDamage3,
                () => 1,
                "WRATH_SERVANT_EVIL_INCARNATION.description"),
            new DebuffIntent());

        // Special check: if pending special attack → use Move4
        var specialCheck = new ConditionalBranchState(SpecialCheckId);
        specialCheck.AddState(evilIncarnation, () => _pendingSpecialAttack);

        // Normal random pool: Move1,Move2,Move3 (Move2 only when staffs exist)
        var normalRand = new RandomBranchState(NormalRandomId);
        normalRand.AddBranch(uuugh, MoveRepeatType.CannotRepeat, 1f);
        normalRand.AddBranch(aaah, MoveRepeatType.CannotRepeat, 1f);
        normalRand.AddBranch(aaaah, MoveRepeatType.CannotRepeat, 1f);

        // If staffs don't exist, use a simpler branch without aaah
        // We'll handle this with a conditional branch
        var noStaffRand = new RandomBranchState("NO_STAFF_RAND");
        noStaffRand.AddBranch(uuugh, MoveRepeatType.CannotRepeat, 1f);
        noStaffRand.AddBranch(aaaah, MoveRepeatType.CannotRepeat, 1f);

        var mainBranch = new ConditionalBranchState(MainBranchId);
        mainBranch.AddState(normalRand, () => WrathServantEncounterHelper.StaffsExist(Creature.CombatState));
        mainBranch.AddState(noStaffRand, () => true); // fallback: no staffs

        specialCheck.AddState(mainBranch, () => true); // fallback: no special pending

        // Follow-ups all loop back to specialCheck
        uuugh.FollowUpState = specialCheck;
        aaah.FollowUpState = specialCheck;
        aaaah.FollowUpState = specialCheck;
        evilIncarnation.FollowUpState = specialCheck;

        return new MonsterMoveStateMachine(
            [uuugh, aaah, aaaah, evilIncarnation, specialCheck, normalRand, noStaffRand, mainBranch],
            specialCheck);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext context,
        Creature creature,
        bool prevented,
        float animLength)
    {
        await base.AfterDeath(context, creature, prevented, animLength);
        if (prevented || Creature.IsDead || creature.Monster is not HermitStaff
            || creature.CombatState != Creature.CombatState
            || WrathServantEncounterHelper.StaffsExist(Creature.CombatState))
        {
            return;
        }

        if (!IsPerformingMove && NextMove.StateId == AaahMoveId
            && MoveStateMachine is { } stateMachine)
        {
            // 手杖全灭后重新经过原有招式分支，移除仅在有手杖时可用的招式。
            stateMachine.ForceCurrentState(stateMachine.States[SpecialCheckId]);
            RollMove(Creature.CombatState!.PlayerCreatures);
        }

        if (Creature.GetCreatureNode() is { } node)
        {
            await node.RefreshIntents();
        }
    }

    // ========== Move Implementations ==========

    /// <summary>呃呃呃！ — 2段攻击目标</summary>
    private async Task UuughMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < UuughHits; i++)
        {
            if (Creature.IsDead) return;
            PlayNormalAttackSfx();
            string animId = i % 2 == 0 ? "AttackStrike" : "AttackSlash";
            await DamageCmd.Attack(UuughDamage)
                .FromMonster(this)
                .WithAttackerAnim(animId, SegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);

            if (i < UuughHits - 1)
            {
                await Cmd.CustomScaledWait(0.04f, 0.08f);
            }
        }
    }

    /// <summary>啊啊啊！ — 2段攻击 + 腐蚀 + 易伤</summary>
    private async Task AaahMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < AaahHits; i++)
        {
            if (Creature.IsDead) return;
            PlayNormalAttackSfx();
            string animId = i % 2 == 0 ? "AttackSlash" : "AttackSlash2";
            AttackCommand attack = await DamageCmd.Attack(AaahDamage)
                .FromMonster(this)
                .WithAttackerAnim(animId, SegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
            await ApplyNextTurnCorrosionToServantTargets(AaahCorrosionPerHit);

            if (i < AaahHits - 1)
            {
                await Cmd.CustomScaledWait(0.04f, 0.08f);
            }
        }

        // 对命中的目标施加腐蚀和易伤
        IReadOnlyList<Creature> hitTargets = WrathServantEncounterHelper.GetServantTargets(Creature.CombatState);
        foreach (Creature target in hitTargets)
        {
            if (target.IsAlive)
            {
                await PowerCmdCompat.Apply<WeakPower>(target, AaahVulnerable, Creature, null);
            }
        }
    }

    /// <summary>啊啊啊啊！ — 3段攻击 + 腐蚀 + 自身力量+2</summary>
    private async Task AaaahMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < AaaahHits; i++)
        {
            PlayNormalAttackSfx();
            string animId = i switch
            {
                0 => "AttackStrike",
                1 => "AttackSlash",
                _ => "AttackSlash2"
            };
            await DamageCmd.Attack(AaaahDamage)
                .FromMonster(this)
                .WithAttackerAnim(animId, SegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
            await ApplyNextTurnCorrosionToServantTargets(AaaahCorrosionPerHit);

            if (i < AaaahHits - 1)
            {
                await Cmd.CustomScaledWait(0.04f, 0.08f);
            }
        }

        // 施加腐蚀 + 自身力量
        await PowerCmdCompat.Apply<StrengthPower>(Creature, AaaahStrength, Creature, null);
    }

    /// <summary>邪恶的化身！！！ — AoE 3段特殊攻击（群体包含玩家）</summary>
    private async Task EvilIncarnationMove(IReadOnlyList<Creature> targets)
    {
        _pendingSpecialAttack = false;
        IReadOnlyList<Creature> groupTargets = GetAllAoeTargets();

        // Segment 1
        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, EvilDamage1, groupTargets);
        LocalOggOneShotPlayer.Play(WrathServantEncounterHelper.ServantSfxRoot + "special_1.ogg", -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, groupTargets))
        {
            await DamageCmd.Attack(EvilDamage1)
                .FromMonster(this)
                .WithAttackerAnim("SpecialS1", SpecialSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .SpawningHitVfxOnEachCreature()
                .WithIndiscriminateBlockBreak(this, EvilDamage1, groupTargets)
                .Execute(null);
        }
        await ApplyNextTurnCorrosionToTargets(groupTargets, EvilCorrosion1);
        await Cmd.CustomScaledWait(0.1f, 0.15f);

        if (Creature.IsDead) return;

        // Segment 2
        groupTargets = GetAllAoeTargets();
        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, EvilDamage2, groupTargets);
        LocalOggOneShotPlayer.Play(WrathServantEncounterHelper.ServantSfxRoot + "special_2.ogg", -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, groupTargets))
        {
            await DamageCmd.Attack(EvilDamage2)
                .FromMonster(this)
                .WithAttackerAnim("SpecialS2", SpecialSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .SpawningHitVfxOnEachCreature()
                .WithIndiscriminateBlockBreak(this, EvilDamage2, groupTargets)
                .Execute(null);
        }
        await ApplyNextTurnCorrosionToTargets(groupTargets, EvilCorrosion2);
        await Cmd.CustomScaledWait(0.1f, 0.15f);

        if (Creature.IsDead) return;

        // Segment 3
        groupTargets = GetAllAoeTargets();
        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, EvilDamage3, groupTargets);
        LocalOggOneShotPlayer.Play(WrathServantEncounterHelper.ServantSfxRoot + "special_end.ogg", -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, groupTargets))
        {
            await DamageCmd.Attack(EvilDamage3)
                .FromMonster(this)
                .WithAttackerAnim("SpecialS3", SpecialSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .SpawningHitVfxOnEachCreature()
                .WithIndiscriminateBlockBreak(this, EvilDamage3, groupTargets)
                .Execute(null);
        }
        await ApplyNextTurnCorrosionToTargets(groupTargets, EvilCorrosion3);
    }

    /// <summary>获取AoE目标：所有玩家 + 敌方怪物（杖+隐士）</summary>
    private IReadOnlyList<Creature> GetAllAoeTargets()
    {
        CombatStateLike? combatState = Creature.CombatState;
        if (combatState == null)
        {
            return [];
        }

        return CombatTargets.DeterministicLiving(
            combatState.Creatures,
            Creature);
    }

    private void PlayNormalAttackSfx()
    {
        string[] sfxFiles =
        [
            WrathServantEncounterHelper.ServantSfxRoot + "attack_strike.ogg",
            WrathServantEncounterHelper.ServantSfxRoot + "attack_slash.ogg",
            WrathServantEncounterHelper.ServantSfxRoot + "attack_thrust.ogg"
        ];
        LocalOggOneShotPlayer.Play(sfxFiles[_normalAttackSfxCursor % sfxFiles.Length], -2f);
        _normalAttackSfxCursor++;
    }

    // ITargetedMonsterAttackProvider — 优先杖 → 隐士
    private Task ApplyNextTurnCorrosionToServantTargets(decimal amount)
    {
        return ApplyNextTurnCorrosionToTargets(
            WrathServantEncounterHelper.GetServantTargets(Creature.CombatState),
            amount);
    }

    private async Task ApplyNextTurnCorrosionToTargets(IEnumerable<Creature> targets, decimal amount)
    {
        foreach (Creature target in targets.Where(static target => target.IsAlive))
        {
            await PowerCmdCompat.Apply<WrathServantNextTurnCorrosionPower>(target, amount, Creature, null);
        }
    }

    public bool UsesTargetedAttackContract(Creature owner)
    {
        return true;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        return WrathServantEncounterHelper.GetServantTargets(owner.CombatState);
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        IReadOnlyList<Creature> targets = GetTargetedAttackTargets(owner);
        return targets.Count > 0 ? targets[0].Name : "Unknown Target";
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new TargetedMonsterAttackIntent(() => UuughDamage, () => UuughHits, "WRATH_SERVANT_UUUGH.description");
        yield return new TargetedMonsterAttackIntent(() => AaahDamage, () => AaahHits, "WRATH_SERVANT_AAAH.description");
        yield return new TargetedMonsterAttackIntent(() => AaaahDamage, () => AaaahHits, "WRATH_SERVANT_AAAAH.description");
        yield return new IndiscriminateAttackIntent(() => EvilDamage1, () => 1, "WRATH_SERVANT_EVIL_INCARNATION.description");
        yield return new IndiscriminateAttackIntent(() => EvilDamage2, () => 1, "WRATH_SERVANT_EVIL_INCARNATION.description");
        yield return new IndiscriminateAttackIntent(() => EvilDamage3, () => 1, "WRATH_SERVANT_EVIL_INCARNATION.description");
        yield return new DebuffIntent();
        yield return new BuffIntent();
    }
}
