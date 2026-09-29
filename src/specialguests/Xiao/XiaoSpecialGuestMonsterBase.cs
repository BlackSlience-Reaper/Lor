using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Xiao;

/// <summary>
/// Xiao-reception-local implementation of the reusable special-guest combat
/// contract.  State is authoritative and saved; UI may only read it.
/// </summary>
public abstract class XiaoSpecialGuestMonsterBase : SpecialGuestMonsterBase
{
    private const string RouterMoveId = "XIAO_GUEST_ROUTER";
    private const string CompositeMoveId = "XIAO_GUEST_COMPOSITE";
    private const string HiddenMoveId = "XIAO_GUEST_HIDDEN";
    private PlannedMoveController<XiaoGuestMove>? _plan;

    protected virtual bool HasStarfirePassive => false;

    protected virtual bool HasIgnitePassive => true;

    protected virtual bool IsSecondStageXiao => false;

    protected virtual bool CanPerformMoves => true;

    protected abstract IReadOnlyList<XiaoGuestMove> GetPattern(
        int patternIndex,
        bool firstTurn);

    protected abstract int PatternLength { get; }

    protected virtual XiaoGuestMove ResolveMoveForCurrentBattle(
        XiaoGuestMove move) => move;

    // 计划存在基类的五个槽位里；实例随怪物克隆丢弃、用到时重建（见 PlannedMoveController）。
    private PlannedMoveController<XiaoGuestMove> Plan => _plan ??= new(
        this,
        StoredIntentSlots,
        slot => (XiaoGuestMove)GetStoredIntent(slot),
        (slot, move) => SetStoredIntent(slot, (int)move),
        (_, move) => CreateIntent(move),
        XiaoGuestMove.None);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await XiaoSpecialGuestPassiveInstaller.EnsureFor(this);
        if (this is XiaoStageOne
            && Creature.CombatState?.Encounter is XiaoSpecialGuestStageOneEncounter)
        {
            XiaoSpecialGuestBgmController.EnsureStageOne(Creature.CombatState);
        }
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (room.Encounter is ISpecialGuestEncounterStage
            {
                SpecialGuestId: XiaoSpecialGuestIds.Guest,
            })
        {
            XiaoSpecialGuestBgmController.Stop();
        }
        await base.AfterCombatEnd(room);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (this is not Miris)
        {
            XiaoSpecialGuestBgmController.RefreshForRound(combatState);
        }

        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        // 计划控制器的委托捕获的是被克隆的实例，克隆体必须用自己的。
        _plan = null;
    }

    // 萧不在 BeforeSideTurnStart 里规划，而在原版 RollMove 经过路由状态时规划（进场时一次，之后每个玩家回合
    // 开始的 PrepareForNextTurn；眩晕的后续行动绕过路由，由 AfterStun 补规划）。隐藏行动只接回自己，假死后不再规划。
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState compositeState = Plan.CreateCompositeState(
            CompositeMoveId,
            PerformCompositeMove);
        MoveState hiddenState = Plan.CreateHiddenState(HiddenMoveId);
        hiddenState.FollowUpState = hiddenState;

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, rng) =>
            {
                PlanNextTurn(rng);
                return CompositeMoveId;
            });
        compositeState.FollowUpState = router;

        return new MonsterMoveStateMachine(
            [compositeState, hiddenState, router],
            !CanPerformMoves
                ? hiddenState
                : HasPlannedMoves ? compositeState : router);
    }

    private bool HasPlannedMoves => HasStoredIntentPlan;

    private void PlanNextTurn(Rng rng)
    {
        // The fixed reception cycle has no random branch, but planning remains
        // on MonsterAi so future variants can add deterministic branches.
        _ = rng;
        var pattern = GetPattern(
            PatternIndex,
            !HasCompletedFirstTurn);
        int count = Math.Min(pattern.Count, Math.Min(IntentCapacity, StoredIntentSlots));
        Plan.WriteSlots(count, slot => ResolveMoveForCurrentBattle(pattern[slot]));

        PatternIndex = (PatternIndex + 1) % Math.Max(1, PatternLength);
        HasCompletedFirstTurn = true;
        RefreshPlannedIntents();
    }

    private async Task PerformCompositeMove(IReadOnlyList<Creature> targets)
    {
        _ = targets;
        if (!CanPerformMoves)
        {
            EnterHiddenIntent();
            return;
        }

        await Plan.PerformPlan(
            () => Creature.IsAlive && CanPerformMoves,
            (_, move) => PerformMove(move));

        ClearPlan();
    }

    private async Task PerformMove(XiaoGuestMove move)
    {
        XiaoMoveDefinition definition = GetMoveDefinition(move);
        foreach (XiaoAttackDefinition attack in definition.Attacks)
        {
            await Attack(
                attack.ResolveDamage(),
                attack.DamageType,
                attack.BurnPerTarget,
                attack.IsIndiscriminate,
                attack.Animation,
                ResolveAttackSfx(attack),
                attack.WindupAnimation,
                attack.WindupSfxFile,
                attack.ImpactSfxFile);
        }

        if (definition.BlockAmount > 0)
        {
            await GainBlock(definition.BlockAmount);
        }

        switch (definition.FollowUp)
        {
            case XiaoMoveFollowUp.BuffAllGuestsPermanently:
                await BuffAllGuestsPermanently();
                break;
            case XiaoMoveFollowUp.BurnAllPlayers:
                await ApplyBurnToAllPlayers(definition.PrimaryEffectAmount);
                break;
            case XiaoMoveFollowUp.EmpowerSelf:
                await LibraryPowerCmd.Apply<LibraryProtectionPower>(
                    Creature,
                    definition.PrimaryEffectAmount,
                    definition.EffectTurns,
                    Creature,
                    null);
                await LibraryPowerCmd.Apply<LibraryStrongPower>(
                    Creature,
                    definition.SecondaryEffectAmount,
                    definition.EffectTurns,
                    Creature,
                    null);
                break;
        }
    }

    private async Task Attack(
        int damage,
        LibraryDamageType type,
        int burnPerTarget,
        bool indiscriminate,
        string animation,
        string sfxFile,
        string? windupAnimation = null,
        string? windupSfxFile = null,
        string? impactSfxFile = null)
    {
        if (Creature.IsDead || Creature.CombatState == null || !CanPerformMoves)
        {
            return;
        }

        IReadOnlyList<Creature> livingPlayers = Creature.CombatState.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .OrderBy(static creature => creature.CombatId)
            .ToArray();
        if (livingPlayers.Count == 0)
        {
            return;
        }

        if (indiscriminate)
        {
            await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(
                this,
                damage,
                livingPlayers,
                suppressNextDamageHook: false);
            await Cmd.Wait(0.9f);
        }

        if (!string.IsNullOrEmpty(windupAnimation))
        {
            if (!string.IsNullOrEmpty(windupSfxFile))
            {
                LocalOggOneShotPlayer.Play(
                    XiaoSpecialGuestIds.CombatAudioRoot + windupSfxFile,
                    -2f);
            }
            await CreatureCmd.TriggerAnim(Creature, windupAnimation, 0.68f);
        }

        LocalOggOneShotPlayer.Play(
            XiaoSpecialGuestIds.CombatAudioRoot + sfxFile,
            -2f);

        // 普通攻击必须走原版 DamageCmd.Attack 管线：RitsuLib 的逐击
        // AttackHitHook（Roland 反击骰子等）只挂在该管线的
        // AttackCommand.Execute 上；LibraryAttackCommand 会绕过它。
        AttackCommand attack = DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(
                animation,
                XiaoAnimationContract.AttackSettlementDelaySeconds)
            .WithHitFx(type switch
            {
                LibraryDamageType.Blunt => "vfx/vfx_attack_blunt",
                _ => "vfx/vfx_attack_slash",
            })
            .SpawningHitVfxOnEachCreature();
        if (!string.IsNullOrEmpty(impactSfxFile))
        {
            attack.AfterAttackerAnim(() =>
            {
                LocalOggOneShotPlayer.Play(
                    XiaoSpecialGuestIds.CombatAudioRoot + impactSfxFile,
                    -2f);
                return Task.CompletedTask;
            });
        }

        AttackCommand command = await attack.Execute(null);
        IReadOnlyList<DamageResult> results = AttackCommandCompat.Results(command)
            .Where(static result => result.Receiver.IsPlayer)
            .ToArray();

        RecordDirectAttackDamageDealt(results);
        if (this is XiaoEgo xiaoEgo)
        {
            xiaoEgo.RecordAttackResults(results);
        }

        foreach (DamageResult result in results)
        {
            Creature target = result.Receiver;
            if (result.TotalDamage <= 0 || !target.IsAlive)
            {
                continue;
            }

            if (burnPerTarget > 0)
            {
                await PowerCmdCompat.Apply<LibraryBurnPower>(
                    target,
                    burnPerTarget,
                    Creature,
                    null);
            }

            if (HasStarfirePassive)
            {
                await PowerCmdCompat.Apply<LibraryBurnPower>(
                    target,
                    XiaoStarfirePassivePower.BurnStacksOnHit,
                    Creature,
                    null);
                await LibraryPowerCmd.Apply<XiaoStarfireStatusPower>(
                    target,
                    1,
                    XiaoStarfirePassivePower.StarfireDurationTurns,
                    Creature,
                    null);
            }

            if (HasIgnitePassive
                && target.GetPower<XiaoIgnitePower>() == null)
            {
                await PowerCmdCompat.Apply<XiaoIgnitePower>(target, 1, Creature, null);
            }
        }
    }

    private string ResolveAttackSfx(XiaoAttackDefinition attack) =>
        this is Miris
            ? attack.MirisSfxFile
            : IsSecondStageXiao
                ? attack.EgoSfxFile
                : attack.StageOneSfxFile;

    private async Task GainBlock(int amount)
    {
        if (Creature.IsDead || !CanPerformMoves)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(
            XiaoSpecialGuestIds.CombatAudioRoot
            + (this is Miris
                ? "Riu_Guard_1.ogg"
                : IsSecondStageXiao ? "Cry_Main_Guard_Win.ogg" : "Riu_Guard.ogg"),
            -2f);
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.65f);
        await CreatureCmd.GainBlock(Creature, amount, ValueProp.Move, null);
    }

    private async Task BuffAllGuestsPermanently()
    {
        if (Creature.CombatState == null)
        {
            return;
        }

        foreach (Creature guest in Creature.CombatState.Enemies
                     .Where(static guest => guest.IsAlive)
                     .OrderBy(static guest => guest.CombatId))
        {
            await LibraryPowerCmd.Apply<LibraryProtectionPower>(guest, 1, 1, Creature, null);
            await LibraryPowerCmd.Apply<LibraryBreakProtectionPower>(guest, 1, 1, Creature, null);
            //await LibraryPowerCmd.Apply<LibraryStrongPower>(guest, 1, 1, Creature, null);
        }
    }

    protected async Task ApplyBurnToAllPlayers(int amount)
    {
        if (Creature.CombatState == null)
        {
            return;
        }

        foreach (Creature player in Creature.CombatState.PlayerCreatures
                     .Where(static player => player.IsAlive)
                     .OrderBy(static player => player.CombatId))
        {
            await PowerCmdCompat.Apply<LibraryBurnPower>(player, amount, Creature, null);
        }
    }

    protected int ScaleThreshold(int baseAmount) =>
        ScaleSpecialGuestAmount(baseAmount);

    protected void EnterHiddenIntent() => Plan.Hide();

    public override async Task AfterStun(Creature creature)
    {
        if (creature == Creature && Creature.IsAlive)
        {
            // The generic stun move's follow-up points straight back at the
            // composite move, bypassing the router. Re-plan immediately so the
            // composite shows a fresh intent plan when the stun ends instead of
            // cleared slots (which render as HiddenIntent) or a stale plan.
            PlanNextTurn(RunRng.MonsterAi);
        }

        await base.AfterStun(creature);
    }

    private AbstractIntent CreateIntent(XiaoGuestMove move)
    {
        XiaoMoveDefinition definition = GetMoveDefinition(move);
        return definition.IntentKind switch
        {
            XiaoMoveIntentKind.DefendBuff =>
                new CombinedDefendBuffIntent(definition.BlockAmount),
            XiaoMoveIntentKind.DefendDebuff =>
                new CombinedDefendDebuffIntent(definition.BlockAmount),
            XiaoMoveIntentKind.AttackDefend =>
                new CombinedAttackDefendIntent(
                    () => definition.ResolveIntentDamage(),
                    () => definition.Attacks.Count,
                    blockAmount: definition.BlockAmount),
            XiaoMoveIntentKind.IndiscriminateAttack =>
                new IndiscriminateAttackIntent(
                    () => definition.ResolveIntentDamage(),
                    () => definition.Attacks.Count,
                    null),
            XiaoMoveIntentKind.SingleAttack =>
                new SingleAttackIntent(() => definition.ResolveIntentDamage()),
            XiaoMoveIntentKind.AttackDebuff =>
                new CombinedAttackDebuffIntent(
                    () => definition.ResolveIntentDamage(),
                    () => definition.Attacks.Count),
            _ => new HiddenIntent(),
        };
    }

    private XiaoMoveDefinition GetMoveDefinition(XiaoGuestMove move) =>
        XiaoMoveDefinitions.Get(move, IsSecondStageXiao);

    private void RefreshPlannedIntents() => Plan.RefreshIntents();

    private void ClearPlan()
    {
        Plan.ClearSlots();
        RefreshPlannedIntents();
    }
}

internal enum XiaoMoveIntentKind
{
    Hidden,
    SingleAttack,
    AttackDebuff,
    AttackDefend,
    DefendBuff,
    DefendDebuff,
    IndiscriminateAttack,
}

internal enum XiaoMoveFollowUp
{
    None,
    BuffAllGuestsPermanently,
    BurnAllPlayers,
    EmpowerSelf,
}

internal readonly record struct XiaoAttackDefinition(
    int LowAscensionDamage,
    int HighAscensionDamage,
    LibraryDamageType DamageType,
    int BurnPerTarget,
    bool IsIndiscriminate,
    string Animation,
    string StageOneSfxFile,
    string MirisSfxFile,
    string EgoSfxFile,
    string? WindupAnimation = null,
    string? WindupSfxFile = null,
    string? ImpactSfxFile = null)
{
    internal int ResolveDamage() =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            HighAscensionDamage,
            LowAscensionDamage);

    internal int ResolveDamageForAscension(bool deadlyEnemies) =>
        deadlyEnemies ? HighAscensionDamage : LowAscensionDamage;
}

internal sealed record XiaoMoveDefinition(
    XiaoMoveIntentKind IntentKind,
    IReadOnlyList<XiaoAttackDefinition> Attacks,
    int BlockAmount = 0,
    XiaoMoveFollowUp FollowUp = XiaoMoveFollowUp.None,
    int PrimaryEffectAmount = 0,
    int SecondaryEffectAmount = 0,
    int EffectTurns = 0)
{
    internal int ResolveIntentDamage() =>
        Attacks.Count == 0 ? 0 : Attacks[0].ResolveDamage();

    internal int ResolveIntentDamageForAscension(bool deadlyEnemies) =>
        Attacks.Count == 0
            ? 0
            : Attacks[0].ResolveDamageForAscension(deadlyEnemies);
}

internal static class XiaoMoveDefinitions
{
    private static readonly XiaoMoveDefinition Hidden = Move(
        XiaoMoveIntentKind.Hidden,
        []);

    private static readonly XiaoMoveDefinition FieryDragonSlashStageTwo = Move(
        XiaoMoveIntentKind.AttackDebuff,
        [
            Segment(2, 5, LibraryDamageType.Slash, 2, false, "Strike", "Xiao_Hori.ogg"),
            Segment(2, 5, LibraryDamageType.Pierce, 2, false, "Penetrate", "Xiao_Stab.ogg"),
            Segment(2, 5, LibraryDamageType.Slash, 2, false, "Strike", "Xiao_Hori.ogg"),
        ]);

    private static readonly IReadOnlyDictionary<XiaoGuestMove, XiaoMoveDefinition> Common =
        new Dictionary<XiaoGuestMove, XiaoMoveDefinition>
        {
            [XiaoGuestMove.LongDrive] = Move(
                XiaoMoveIntentKind.SingleAttack,
                [
                    Segment(
                        4,
                        5,
                        LibraryDamageType.Pierce,
                        0,
                        false,
                        "Slash",
                        "Philip_Vert.ogg",
                        "Riu_Vert.ogg",
                        "Xiao_Vert.ogg"),
                ]),
            [XiaoGuestMove.ThroatPierce] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(
                        3,
                        4,
                        LibraryDamageType.Pierce,
                        1,
                        false,
                        "Penetrate",
                        "Philip_Stab.ogg",
                        "Riu_Stab.ogg",
                        "Xiao_Stab.ogg"),
                ]),
            [XiaoGuestMove.Duel] = Move(
                XiaoMoveIntentKind.AttackDefend,
                [
                    Segment(
                        9,
                        10,
                        LibraryDamageType.Slash,
                        0,
                        false,
                        "Strike",
                        "Philip_Hori.ogg",
                        "Riu_Hori.ogg",
                        "Xiao_Hori.ogg"),
                ],
                blockAmount: 13),
            [XiaoGuestMove.FieryDragonSlash] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(
                        9,
                        10,
                        LibraryDamageType.Slash,
                        1,
                        false,
                        "Strike",
                        "Philip_Hori.ogg"),
                ]),
            [XiaoGuestMove.FervidEmotion] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(
                        10,
                        12,
                        LibraryDamageType.Pierce,
                        2,
                        false,
                        "Strike",
                        "Philip_Hori.ogg",
                        "Riu_Hori.ogg",
                        "Xiao_Hori.ogg"),
                ]),
            [XiaoGuestMove.BlazingDance] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(
                        4,
                        6,
                        LibraryDamageType.Slash,
                        2,
                        false,
                        "Slash",
                        "Philip_Vert.ogg",
                        "Riu_Vert.ogg",
                        "Xiao_Vert.ogg"),
                    Segment(
                        4,
                        6,
                        LibraryDamageType.Blunt,
                        2,
                        false,
                        "Strike",
                        "Philip_Hori.ogg",
                        "Riu_Hori.ogg",
                        "Xiao_Hori.ogg"),
                ]),
            [XiaoGuestMove.DoubleFlank] = Move(
                XiaoMoveIntentKind.DefendBuff,
                [],
                blockAmount: 12,
                followUp: XiaoMoveFollowUp.BuffAllGuestsPermanently),
            [XiaoGuestMove.HotBlood] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(
                        5,
                        7,
                        LibraryDamageType.Slash,
                        1,
                        false,
                        "Slash",
                        "Philip_Vert.ogg",
                        "Riu_Vert.ogg",
                        "Xiao_Vert.ogg"),
                    Segment(
                        5,
                        7,
                        LibraryDamageType.Blunt,
                        1,
                        false,
                        "Strike",
                        "Philip_Hori.ogg",
                        "Riu_Hori.ogg",
                        "Xiao_Hori.ogg"),
                ]),
            [XiaoGuestMove.GreatFlame] = Move(
                XiaoMoveIntentKind.IndiscriminateAttack,
                [
                    Segment(
                        14,
                        17,
                        LibraryDamageType.Slash,
                        3,
                        true,
                        "S2",
                        "Philip_Strong.ogg",
                        windupAnimation: "S1",
                        windupSfxFile: "Riu_Shao_UpperAtk.ogg"),
                ]),
            [XiaoGuestMove.BixueDanxin] = Move(
                XiaoMoveIntentKind.AttackDefend,
                [
                    Segment(
                        4,
                        6,
                        LibraryDamageType.Slash,
                        0,
                        false,
                        "Penetrate",
                        "Riu_Stab.ogg"),
                ],
                blockAmount: 9),
            [XiaoGuestMove.FlameDragonFist] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(
                        17,
                        20,
                        LibraryDamageType.Blunt,
                        4,
                        false,
                        "S2",
                        "Riu_Strong.ogg",
                        windupAnimation: "S1",
                        windupSfxFile: "Riu_Hori.ogg"),
                ]),
            [XiaoGuestMove.SkywardFlame] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(9, 11, LibraryDamageType.Slash, 1, false, "Slash", "Xiao_Vert.ogg"),
                ]),
            [XiaoGuestMove.BreakBamboo] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(5, 6, LibraryDamageType.Blunt, 1, false, "Strike", "Xiao_Hori.ogg"),
                    Segment(5, 6, LibraryDamageType.Pierce, 1, false, "Penetrate", "Xiao_Stab.ogg"),
                ]),
            [XiaoGuestMove.JiaotuSuppressEvil] = Move(
                XiaoMoveIntentKind.DefendDebuff,
                [],
                blockAmount: 20,
                followUp: XiaoMoveFollowUp.BurnAllPlayers,
                primaryEffectAmount: 1),
            [XiaoGuestMove.BianDispute] = Move(
                XiaoMoveIntentKind.DefendBuff,
                [],
                blockAmount: 40,
                followUp: XiaoMoveFollowUp.EmpowerSelf,
                primaryEffectAmount: 3,
                secondaryEffectAmount: 2,
                effectTurns: 2),
            [XiaoGuestMove.ChiwenSwallowRidge] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(6, 7, LibraryDamageType.Slash, 3, false, "Strike", "Xiao_Hori.ogg"),
                    Segment(6, 7, LibraryDamageType.Blunt, 3, false, "Slash", "Xiao_Vert.ogg"),
                ]),
            [XiaoGuestMove.YaziVengeance] = Move(
                XiaoMoveIntentKind.IndiscriminateAttack,
                [
                    Segment(
                        11,
                        13,
                        LibraryDamageType.Blunt,
                        2,
                        true,
                        "S4",
                        "Xiao_LandHit_Hit.ogg",
                        windupAnimation: "S3",
                        windupSfxFile: "Xiao_LandHit_Charge.ogg"),
                ]),
            [XiaoGuestMove.SuanniSoaringCloud] = Move(
                XiaoMoveIntentKind.AttackDebuff,
                [
                    Segment(
                        12,
                        16,
                        LibraryDamageType.Pierce,
                        2,
                        false,
                        "S5",
                        "Xiao_DrangonStab.ogg"),
                ]),
            [XiaoGuestMove.TaotieFeast] = Move(
                XiaoMoveIntentKind.IndiscriminateAttack,
                [
                    Segment(
                        16,
                        18,
                        LibraryDamageType.Slash,
                        6,
                        true,
                        "Special",
                        "Xiao_DragonUp_Start.ogg",
                        impactSfxFile: "Xiao_DragonUp_End.ogg"),
                ]),
        };

    internal static XiaoMoveDefinition Get(
        XiaoGuestMove move,
        bool isSecondStageXiao)
    {
        if (move == XiaoGuestMove.FieryDragonSlash && isSecondStageXiao)
        {
            return FieryDragonSlashStageTwo;
        }

        return Common.TryGetValue(move, out XiaoMoveDefinition? definition)
            ? definition
            : Hidden;
    }

    private static XiaoMoveDefinition Move(
        XiaoMoveIntentKind intentKind,
        IReadOnlyList<XiaoAttackDefinition> attacks,
        int blockAmount = 0,
        XiaoMoveFollowUp followUp = XiaoMoveFollowUp.None,
        int primaryEffectAmount = 0,
        int secondaryEffectAmount = 0,
        int effectTurns = 0)
    {
        if (attacks.Count > 1
            && attacks.Skip(1).Any(attack =>
                attack.LowAscensionDamage != attacks[0].LowAscensionDamage
                || attack.HighAscensionDamage != attacks[0].HighAscensionDamage))
        {
            throw new ArgumentException(
                "A Xiao move with one damage intent must use one damage pair for every attack segment.",
                nameof(attacks));
        }

        return new(
            intentKind,
            attacks,
            blockAmount,
            followUp,
            primaryEffectAmount,
            secondaryEffectAmount,
            effectTurns);
    }

    private static XiaoAttackDefinition Segment(
        int lowAscensionDamage,
        int highAscensionDamage,
        LibraryDamageType damageType,
        int burnPerTarget,
        bool isIndiscriminate,
        string animation,
        string stageOneSfxFile,
        string? mirisSfxFile = null,
        string? egoSfxFile = null,
        string? windupAnimation = null,
        string? windupSfxFile = null,
        string? impactSfxFile = null) =>
        new(
            lowAscensionDamage,
            highAscensionDamage,
            damageType,
            burnPerTarget,
            isIndiscriminate,
            animation,
            stageOneSfxFile,
            mirisSfxFile ?? stageOneSfxFile,
            egoSfxFile ?? stageOneSfxFile,
            windupAnimation,
            windupSfxFile,
            impactSfxFile);
}
