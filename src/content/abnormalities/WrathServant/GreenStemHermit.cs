using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

/// <summary>
/// 青林隐士 — 只能被愤怒侍从击杀的主要目标。
/// Phase 1 (杖存在): 攻击玩家
///   Pattern A: 起开×2 → 呼呼呼×1
///   Pattern B: 老实待着×1 → 呼呼呼×1 → 起开×1
///   交替 A→B→A→B...
/// Phase 2 (无杖): 攻击侍从
///   Pattern C: 朋友啊×3
///   Pattern D: 汝终将崩溃×3 → 起来吧×1
///   交替 C→D→C→D...
/// </summary>
public sealed class GreenStemHermit : CounterIntentMonsterModel, ITargetedMonsterAttackProvider
{
    private const string GetAwayMoveId = "GET_AWAY";
    private const string HuffMoveId = "HUFF";
    private const string StayPutMoveId = "STAY_PUT";
    private const string MyFriendMoveId = "MY_FRIEND";
    private const string YouWillCrumbleMoveId = "YOU_WILL_CRUMBLE";
    private const string RiseUpMoveId = "RISE_UP";
    private const string PhaseCheckId = "PHASE_CHECK";
    private const string Phase1SequenceId = "PHASE1_SEQ";
    private const string Phase2SequenceId = "PHASE2_SEQ";

    private const int GetAwayBlock = 18;

    private  int GetAwayCounterDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);
    
    private const int GetAwayHits = 1;
    private const int MyFriendHits = 3;
    private const int YouWillCrumbleHits = 3;
    private const int RiseUpStaffCount = 1;
    private const int HuffStrength = 3;
    private const int HuffEndurance = 5;
    private const int HuffEnduranceTurns = 2;
    private const int StayPutHits = 2;
    private const int StayPutHeal = 11;
    private const float SegmentDelaySeconds = 0.48f;
    private const float GroundSlamDelaySeconds = 1.5f;

    public const string IdleTexturePath = WrathServantAssets.GreenStemHermitMonsterRoot + "idle.png";
    public const string HitTexturePath = WrathServantAssets.GreenStemHermitMonsterRoot + "hit.png";
    public const string ReachTexturePath = WrathServantAssets.GreenStemHermitMonsterRoot + "reach.png";
    public const string GroundTexturePath = WrathServantAssets.GreenStemHermitMonsterRoot + "ground.png";
    public const string ThrustTexturePath = WrathServantAssets.GreenStemHermitMonsterRoot + "thrust.png";

    private static readonly string[] SfxPaths =
    [
        WrathServantEncounterHelper.HermitSfxRoot + "attack.ogg",
        WrathServantEncounterHelper.HermitSfxRoot + "strong_attack.ogg",
        WrathServantEncounterHelper.HermitSfxRoot + "ground.ogg"
    ];

    public static readonly IReadOnlyList<string> AssetPathsStatic =
        GreenStemHermitCreatureVisuals.AssetPaths
            .Concat(SfxPaths)
            .ToArray();

    /// <summary>
    /// Phase 1 步骤计数器:
    /// Pattern A: 0=起开, 1=起开, 2=呼呼呼
    /// Pattern B: 3=老实待着, 4=呼呼呼, 5=起开
    /// 循环: 0-5 → reset to 0
    /// </summary>
    private int _phase1Step;

    /// <summary>
    /// Phase 2 步骤计数器:
    /// 0=朋友啊×3, 1=汝终将崩溃×3, 2=起来吧
    /// 循环: 0-2 → reset to 0
    /// </summary>
    private int _phase2Step;

    private bool _inPhase2;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 386, 371);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 390, 375);

    public override int DefaultChaoResistance => 160;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Resist
    };

    // Move damage values
    private int GetAwayDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies,20, 19);

    private int StayPutDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12);

    private int MyFriendDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 15, 14);

    private int YouWillCrumbleDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 25, 24);

    private int RiseUpDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 30, 28);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>();
            paths.AddRange(
                GreenStemHermitCreatureVisuals.AssetPaths);
            paths.AddRange(SfxPaths);

            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _phase1Step = 0;
        _phase2Step = 0;
        _inPhase2 = false;
        ClearCounterIntentQueueAndRefresh();
        WrathServantDeathContext.Clear();

        EncounterBgmController.RegisterMonster(Creature);

        // 应用能力
        await PowerCmdCompat.Apply<GreenStemHermitProtectionPower>(Creature, 1m, Creature, null, silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // Phase 1 moves (attack players)
        var getAway = new MoveState(
            GetAwayMoveId,
            GetAwayMove,
            CreateGetAwayIntent());

        var huff = new MoveState(
            HuffMoveId,
            HuffMove,
            new BuffIntent());

        var stayPut = new MoveState(
            StayPutMoveId,
            StayPutMove,
            new IndiscriminateAttackIntent(() => StayPutDamage, () => StayPutHits, "GREEN_STEM_HERMIT_STAY_PUT.description"),
            new HealIntent());

        // Phase 2 moves (attack servant) - 3 intents shown simultaneously
        var myFriend = new MoveState(
            MyFriendMoveId,
            MyFriendMove,
            new TargetedMonsterAttackIntent(
                () => MyFriendDamage,
                () => 1,
                "GREEN_STEM_HERMIT_MY_FRIEND.description"),
            new TargetedMonsterAttackIntent(
                () => MyFriendDamage,
                () => 1,
                "GREEN_STEM_HERMIT_MY_FRIEND.description"),
            new TargetedMonsterAttackIntent(
                () => MyFriendDamage,
                () => 1,
                "GREEN_STEM_HERMIT_MY_FRIEND.description"));

        var youWillCrumble = new MoveState(
            YouWillCrumbleMoveId,
            YouWillCrumbleMove,
            new TargetedMonsterAttackIntent(
                () => YouWillCrumbleDamage,
                () => 1,
                "GREEN_STEM_HERMIT_YOU_WILL_CRUMBLE.description"),
            new TargetedMonsterAttackIntent(
                () => YouWillCrumbleDamage,
                () => 1,
                "GREEN_STEM_HERMIT_YOU_WILL_CRUMBLE.description"),
            new TargetedMonsterAttackIntent(
                () => YouWillCrumbleDamage,
                () => 1,
                "GREEN_STEM_HERMIT_YOU_WILL_CRUMBLE.description"),
            new DebuffIntent());

        var riseUp = new MoveState(
            RiseUpMoveId,
            RiseUpMove,
            new IndiscriminateAttackIntent(() => RiseUpDamage, () => 1, "GREEN_STEM_HERMIT_RISE_UP.description"),
            new SummonIntent());

        // Phase check: branch based on staff existence
        var phaseCheck = new ConditionalBranchState(PhaseCheckId);

        // Phase 1 sequence (implemented as conditional move dispatch)
        var phase1Seq = new ConditionalBranchState(Phase1SequenceId);
        phase1Seq.AddState(getAway, () => _phase1Step is 0 or 1 or 5);
        phase1Seq.AddState(huff, () => _phase1Step is 2 or 4);
        phase1Seq.AddState(stayPut, () => _phase1Step == 3);
        phase1Seq.AddState(getAway, () => true); // fallback

        // Phase 2 sequence: myFriend → youWillCrumble → riseUp → loop
        var phase2Seq = new ConditionalBranchState(Phase2SequenceId);
        phase2Seq.AddState(myFriend, () => _phase2Step == 0);
        phase2Seq.AddState(youWillCrumble, () => _phase2Step == 1);
        phase2Seq.AddState(riseUp, () => _phase2Step == 2);
        phase2Seq.AddState(myFriend, () => true); // fallback

        phaseCheck.AddState(phase2Seq, () => _inPhase2 || !WrathServantEncounterHelper.StaffsExist(Creature.CombatState));
        phaseCheck.AddState(phase1Seq, () => true); // fallback = phase1

        // 每个 MoveState 结束后回到 phaseCheck
        getAway.FollowUpState = phaseCheck;
        huff.FollowUpState = phaseCheck;
        stayPut.FollowUpState = phaseCheck;
        myFriend.FollowUpState = phaseCheck;
        youWillCrumble.FollowUpState = phaseCheck;
        riseUp.FollowUpState = phaseCheck;

        return new MonsterMoveStateMachine(
            [getAway, huff, stayPut, myFriend, youWillCrumble, riseUp, phaseCheck, phase1Seq, phase2Seq],
            phaseCheck);
    }

    /// <summary>
    /// 招式执行后推进步骤计数器（在招式方法末尾调用）
    /// </summary>
    private void AdvancePhase1Step()
    {
        _phase1Step = (_phase1Step + 1) % 6;
    }

    private void AdvancePhase2Step()
    {
        _phase2Step = (_phase2Step + 1) % 3;
    }

    private void CheckPhaseTransition()
    {
        if (!_inPhase2 && !WrathServantEncounterHelper.StaffsExist(Creature.CombatState))
        {
            _inPhase2 = true;
            _phase2Step = 0;
        }
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

        // 保留当前招式；无杖状态只刷新攻击玩家的目标，后续招式由阶段路由选择。
        CheckPhaseTransition();

        if (Creature.GetCreatureNode() is { } node)
        {
            await node.RefreshIntents();
        }
    }

    // ========== Phase 1 Moves ==========

    /// <summary>起开 — 单段攻击玩家</summary>
    private async Task GetAwayMove(IReadOnlyList<Creature> targets)
    {
        CheckPhaseTransition();
        ClearCounterIntentQueueAndRefresh();
        LocalOggOneShotPlayer.Play(WrathServantEncounterHelper.HermitSfxRoot + "attack.ogg", -2f);
        await DamageCmd.Attack(GetAwayDamage)
            .FromMonster(this)
            .WithAttackerAnim("AttackReach", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await CreatureCmd.GainBlock(Creature, GetAwayBlock, ValueProp.Move, null);
        AdvancePhase1Step();
    }

    // private async Task PerformGetAwayCounterAttack(PlayerChoiceContext choiceContext, int damage, Creature counterTarget)
    // {
    //     if (Creature.IsDead || counterTarget.IsDead)
    //     {
    //         return;
    //     }

    //     LocalOggOneShotPlayer.Play(WrathServantEncounterHelper.HermitSfxRoot + "attack.ogg", -2f);
    //     using (TargetedMonsterAttackHelper.ForceTargets(Creature, [counterTarget]))
    //     {
    //         await DamageCmd.Attack(damage)
    //             .FromMonster(this)
    //             .WithAttackerAnim("AttackReach", SegmentDelaySeconds)
    //             .WithHitFx("vfx/vfx_attack_blunt")
    //             .Execute(choiceContext);
    //     }
    // }

    /// <summary>呼呼呼…… — 3段攻击玩家</summary>
    private async Task HuffMove(IReadOnlyList<Creature> targets)
    {
        CheckPhaseTransition();
        await CreatureCmd.TriggerAnim(Creature, "AttackGround", SegmentDelaySeconds);

        IReadOnlyList<Creature> otherEnemies = CombatState.Enemies
            .Where(enemy => enemy.IsAlive && enemy != Creature && enemy.Monster is not WrathServant)
            .ToArray();

        if (otherEnemies.Count > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(otherEnemies, HuffStrength, Creature, null);
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                new ThrowingPlayerChoiceContext(),
                otherEnemies,
                HuffEndurance,
                HuffEnduranceTurns,
                IsPermanent: false,
                Creature,
                null);
        }

        AdvancePhase1Step();
    }

    /// <summary>老实待着 — 群体攻击(含玩家+侍从) + 回复所有敌方</summary>
    private async Task StayPutMove(IReadOnlyList<Creature> targets)
    {
        CheckPhaseTransition();
        IReadOnlyList<Creature> groupTargets = WrathServantEncounterHelper.GetHermitGroupTargets(Creature.CombatState);

        for (int i = 0; i < StayPutHits; i++)
        {
            if (Creature.IsDead) return;
            await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, StayPutDamage, groupTargets);
            LocalOggOneShotPlayer.Play(
                WrathServantEncounterHelper.HermitSfxRoot + (i == 0 ? "strong_attack.ogg" : "ground.ogg"),
                -2f);
            using (TargetedMonsterAttackHelper.ForceTargets(Creature, groupTargets))
            {
                await DamageCmd.Attack(StayPutDamage)
                    .FromMonster(this)
                    .WithAttackerAnim("AttackThrust", SegmentDelaySeconds)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .SpawningHitVfxOnEachCreature()
                    .WithIndiscriminateBlockBreak(this, StayPutDamage, groupTargets)
                    .Execute(null);
            }

            if (i < StayPutHits - 1)
            {
                await Cmd.CustomScaledWait(0.04f, 0.08f);
            }
        }

        foreach (Creature enemy in CombatState.Enemies.Where(enemy => enemy.IsAlive && enemy.Monster is not WrathServant).ToArray())
        {
            await CreatureCmd.Heal(enemy, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(enemy, StayPutHeal));
            if (enemy is LibraryCreature libraryCreature && libraryCreature.HasChaoResistance)
            {
                await LibraryCreatureCmd.HealChaoValue(libraryCreature, StayPutHeal);
            }
        }

        AdvancePhase1Step();
    }

    // ========== Phase 2 Moves ==========

    /// <summary>朋友啊……！ — 3段攻击侍从</summary>
    private async Task MyFriendMove(IReadOnlyList<Creature> targets)
    {
        CheckPhaseTransition();

        for (int i = 0; i < MyFriendHits; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(WrathServantEncounterHelper.HermitSfxRoot + "attack.ogg", -2f);
            await DamageCmd.Attack(MyFriendDamage)
                .FromMonster(this)
                .WithAttackerAnim("AttackThrust", SegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null);

            if (i < MyFriendHits - 1)
            {
                await Cmd.CustomScaledWait(0.04f, 0.08f);
            }
        }

        Creature? servant = WrathServantEncounterHelper.FindServant(Creature.CombatState);
        if (servant != null)
        {
            await PowerCmdCompat.Apply<WrathServantStaffMarkPower>(servant, 1m, Creature, null);
        }

        AdvancePhase2Step();
    }

    /// <summary>
    /// 汝终将崩溃……！ — 3段攻击侍从，附带手杖加成
    /// 读取目标的 StaffMarkPower.Amount × 3 加到攻击伤害
    /// </summary>
    private async Task YouWillCrumbleMove(IReadOnlyList<Creature> targets)
    {
        CheckPhaseTransition();

        Creature? servant = WrathServantEncounterHelper.FindServant(Creature.CombatState);
        int bonusDamage = 0;
        if (servant != null)
        {
            WrathServantStaffMarkPower? staffMark = servant.GetPower<WrathServantStaffMarkPower>();
            if (staffMark != null && staffMark.Amount > 0)
            {
                bonusDamage = staffMark.Amount * WrathServantStaffMarkPower.DamageMultiplier;
            }
        }

        int totalDamage = YouWillCrumbleDamage + bonusDamage;

        for (int i = 0; i < YouWillCrumbleHits; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(WrathServantEncounterHelper.HermitSfxRoot + "strong_attack.ogg", -2f);
            await DamageCmd.Attack(totalDamage)
                .FromMonster(this)
                .WithAttackerAnim("AttackThrust", SegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null);

            if (i < YouWillCrumbleHits - 1)
            {
                await Cmd.CustomScaledWait(0.04f, 0.08f);
            }
        }

        AdvancePhase2Step();
    }

    /// <summary>起来吧！ — 群体攻击 + 召唤2个隐士之杖到空槽位</summary>
    private async Task RiseUpMove(IReadOnlyList<Creature> targets)
    {
        CheckPhaseTransition();

        IReadOnlyList<Creature> groupTargets = WrathServantEncounterHelper.GetHermitGroupTargets(Creature.CombatState);
        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, RiseUpDamage, groupTargets);
        LocalOggOneShotPlayer.Play(WrathServantEncounterHelper.HermitSfxRoot + "ground.ogg", -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, groupTargets))
        {
            await DamageCmd.Attack(RiseUpDamage)
                .FromMonster(this)
                .WithAttackerAnim("AttackGround", SegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_blunt")
                .SpawningHitVfxOnEachCreature()
                .WithIndiscriminateBlockBreak(this, RiseUpDamage, groupTargets)
                .Execute(null);
        }

        await Cmd.CustomScaledWait(GroundSlamDelaySeconds, GroundSlamDelaySeconds);

        // 召唤杖到空槽位
        CombatStateLike? combatState = Creature.CombatState;
        if (combatState != null)
        {
            string[] staffSlots = [WrathServantStrong.StaffSlotLeft, WrathServantStrong.StaffSlotRight];
            int summoned = 0;
            foreach (string slotId in staffSlots)
            {
                if (summoned >= RiseUpStaffCount)
                {
                    break;
                }

                bool slotOccupied = combatState.Creatures.Any(c =>
                    c.IsAlive && c.SlotName == slotId);

                if (!slotOccupied)
                {
                    MonsterModel staffModel = ModelDb.Monster<HermitStaff>().ToMutable();
                    Creature staffCreature = await CreatureCmd.Add(staffModel, combatState, CombatSide.Enemy, slotId);
                    staffCreature.PrepareForNextTurn(combatState.PlayerCreatures);
                    summoned++;
                }
            }
        }

        _inPhase2 = false;
        _phase1Step = 0;
        AdvancePhase2Step();
    }

    // ITargetedMonsterAttackProvider
    public bool UsesTargetedAttackContract(Creature owner)
    {
        // Phase 2 attacks target WrathServant
        return _inPhase2 || !WrathServantEncounterHelper.StaffsExist(owner.CombatState);
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        Creature? servant = WrathServantEncounterHelper.FindServant(owner.CombatState);
        return servant != null ? [servant] : [];
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        Creature? servant = WrathServantEncounterHelper.FindServant(owner.CombatState);
        return servant?.Name ?? "Unknown Target";
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return CreateGetAwayIntent();
        // yield return new CounterAttackIntent(GetAwayCounterDamage);
        // yield return new CounterAttackIntent(GetAwayCounterDamage);
        // yield return new CounterAttackIntent(GetAwayCounterDamage);
        yield return new BuffIntent();
        yield return new IndiscriminateAttackIntent(() => StayPutDamage, () => StayPutHits, "GREEN_STEM_HERMIT_STAY_PUT.description");
        yield return new HealIntent();
        yield return new TargetedMonsterAttackIntent(() => MyFriendDamage, () => 1, "GREEN_STEM_HERMIT_MY_FRIEND.description");
        yield return new TargetedMonsterAttackIntent(() => YouWillCrumbleDamage, () => 1, "GREEN_STEM_HERMIT_YOU_WILL_CRUMBLE.description");
        yield return new IndiscriminateAttackIntent(() => RiseUpDamage, () => 1, "GREEN_STEM_HERMIT_RISE_UP.description");
        yield return new SummonIntent();
        yield return new DebuffIntent();
    }

    private AbstractIntent CreateGetAwayIntent() =>
        new CombinedTargetedAttackDefendIntent(
            () => GetAwayDamage,
            () => GetAwayHits,
            "NATURAL_HERMIT_GET_AWAY_TARGETED.description",
            "GREEN_STEM_HERMIT_GET_AWAY.description",
            true,
            GetAwayBlock);
}
