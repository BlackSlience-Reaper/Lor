using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryLib.Hooks;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;

public sealed class WarmheartedWoodsman : LorMonsterModel, ITargetedMonsterAttackProvider
{
    internal const string EmptyHeartMoveId = "EMPTY_HEART";
    internal const string GiantAxeSlashMoveId = "GIANT_AXE_SLASH";
    internal const string DoNotTakeMyHeartMoveId = "DO_NOT_TAKE_MY_HEART";
    internal const string WarmHeartbeatMoveId = "WARM_HEARTBEAT";
    internal const string FierceRumbleMoveId = "FIERCE_RUMBLE";
    internal const string LoggingMoveId = "LOGGING";
    private const string RouterStateId = "WARMHEARTED_WOODSMAN_ROUTER";

    private const int BaseMinHp = 560; // 热心的樵夫：低于 ToughEnemies 进阶时的最低初始生命。
    private const int BaseMaxHp = 565; // 热心的樵夫：低于 ToughEnemies 进阶时的最高初始生命。
    private const int HighAscensionMinHp = 667; // 热心的樵夫：达到 ToughEnemies 进阶时的最低初始生命。
    private const int HighAscensionMaxHp = 670; // 热心的樵夫：达到 ToughEnemies 进阶时的最高初始生命。
    private const int ChaoResistance = 230; // 热心的樵夫：混乱抗性上限。

    public const int WarmHeartEnergyPerStack = 1; // 温暖的心：每层在玩家回合开始时为每名玩家恢复的能量。
    private const int WarmHeartPlaceholderStacks = 1; // 温暖的心：0 层无法施加，开局先以此层数挂上再静默归零。
    public const decimal ViolentHeartTreeDamageMultiplier = 2m; // 暴跳之心：攻击树木时的伤害倍率。
    public const int ViolentHeartHealPercent = 10; // 暴跳之心：击杀树木时恢复的最大生命百分比。
    public const int ViolentHeartStrength = 4; // 暴跳之心：击杀树木时获得的力量层数。
    public const int ViolentHeartWarmHeartGain = 2; // 暴跳之心：击杀树木时获得的温暖的心层数。
    public const int InitialTreeReviveCharges = 2; // 心脏：开局时树木可重新生成的次数。
    public const int TreeRespawnChargeCost = 1; // 心脏：每次重新生成树木消耗的层数。

    internal const int EmptyHeartCycleTurns = 6; // 空洞之心：第 1 回合固定使用，此后每隔该回合数固定使用一次。
    internal const int EmptyHeartBlock = 99; // 空洞之心：获得的格挡。
    internal const int EmptyHeartTemporaryThorns = 20; // 空洞之心：获得的荆棘层数，下回合开始时移除。
    internal const int GiantAxeSlashBaseDamage = 16; // 巨斧劈砍：低于 DeadlyEnemies 进阶时的单次伤害。
    internal const int GiantAxeSlashHighAscensionDamage = 18; // 巨斧劈砍：达到 DeadlyEnemies 进阶时的单次伤害。
    internal const int GiantAxeSlashHits = 2; // 巨斧劈砍：攻击次数。
    internal const int DoNotTakeMyHeartBaseDamage = 27; // 不要夺走我的心……：低于 DeadlyEnemies 进阶时的单次伤害。
    internal const int DoNotTakeMyHeartHighAscensionDamage = 33; // 不要夺走我的心……：达到 DeadlyEnemies 进阶时的单次伤害。
    internal const int DoNotTakeMyHeartBlock = 40; // 不要夺走我的心……：攻击后获得的格挡。
    internal const int WarmHeartbeatBaseDamage = 23; // 温暖的心跳：低于 DeadlyEnemies 进阶时的单次伤害。
    internal const int WarmHeartbeatHighAscensionDamage = 24; // 温暖的心跳：达到 DeadlyEnemies 进阶时的单次伤害。
    internal const int WarmHeartbeatEndurance = 2; // 温暖的心跳：攻击后获得的忍耐层数。
    internal const int WarmHeartbeatEnduranceTurns = 3; // 温暖的心跳：忍耐的持续回合数。
    internal const int FierceRumbleBaseDamage = 24; // 猛烈的轰鸣：低于 DeadlyEnemies 进阶时的单次伤害。
    internal const int FierceRumbleHighAscensionDamage = 29; // 猛烈的轰鸣：达到 DeadlyEnemies 进阶时的单次伤害。
    internal const int FierceRumbleBlock = 43; // 猛烈的轰鸣：攻击后获得的格挡。
    internal const int LoggingBaseDamage = 8; // 伐木：低于 DeadlyEnemies 进阶时的单次伤害。
    internal const int LoggingHighAscensionDamage = 12; // 伐木：达到 DeadlyEnemies 进阶时的单次伤害。
    internal const int LoggingHits = 3; // 伐木：攻击次数。
    internal const int LoggingChancePercentWithTree = 60; // 伐木：有存活树木时的选用概率（百分比），其余从招式 2–5 中随机。
    internal const int LoggingChancePercentWithoutTree = 20; // 伐木：没有存活树木时的选用概率（百分比），其余从招式 2–5 中随机。
    private const int ChancePercentRange = 100; // 伐木：概率掷骰的取值范围 [0, 100)。

    internal const string TextureRoot = "res://images/monsters/warmhearted_woodsman/";
    internal const string EmptyIdleTexturePath = TextureRoot + "idle_empty.png";
    internal const string WarmIdleTexturePath = TextureRoot + "idle_warm.png";
    internal const string LoggingFinalTexturePath = TextureRoot + "logging_final.png";
    internal const string AttackBluntTexturePath = TextureRoot + "attack_blunt.png";
    internal const string AttackSlashTexturePath = TextureRoot + "attack_slash.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";
    internal const string GuardTexturePath = TextureRoot + "guard.png";
    internal const string SfxRoot = "res://audio/sfx/warmhearted_woodsman/";
    internal const string AttackSfxPath = SfxRoot + "attack.ogg";
    internal const string KillSfxPath = SfxRoot + "kill.ogg";
    internal const string WarmAmbientSfxPath = SfxRoot + "warm_ambient.ogg";

    private static readonly string[] AdditionalAssetPaths =
    [
        AttackSfxPath,
        KillSfxPath,
        WarmAmbientSfxPath,
        "res://images/powers/warm_heart_power.png",
        "res://images/powers/warmhearted_woodsman_violent_heart_passive_power.png",
        "res://images/powers/warmhearted_woodsman_temporary_thorns_power.png"
    ];

    private static readonly string PageRelicTitleLocKey = $"{ModelDb.GetId<WarmheartedWoodsmanPageRelic>().Entry}.title";

    // 招式 2–5：伐木之外的随机候选，不能连续两回合选中同一招。
    private static readonly string[] RandomMoveIds =
    [
        GiantAxeSlashMoveId,
        DoNotTakeMyHeartMoveId,
        WarmHeartbeatMoveId,
        FierceRumbleMoveId
    ];

    private Dictionary<string, MoveState> _statesById = [];
    private string? _lastMoveId;
    private int _treeReviveCharges = InitialTreeReviveCharges;
    private bool _treeRespawnPending;
    private LocalOggLoopPlayer.LoopHandle? _warmAmbientLoop;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
        _warmAmbientLoop = null;
    }

    public bool HasWarmHeart => Creature?.GetPowerAmount<WarmHeartPower>() > 0;

    public int TreeReviveCharges => _treeReviveCharges;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMinHp, BaseMinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMaxHp, BaseMaxHp);

    public override int DefaultChaoResistance => ChaoResistance;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override IEnumerable<string> AssetPaths =>
        WarmheartedWoodsmanCreatureVisuals.Profile.AssetPaths
            .Concat(AdditionalAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _lastMoveId = null;
        _treeReviveCharges = InitialTreeReviveCharges;
        _treeRespawnPending = false;
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<WarmheartedWoodsmanViolentHeartPassivePower>(Creature, 1, Creature, null, silent: true);
        await ApplyInitialWarmHeart();
        await PowerCmdCompat.Apply<LibraryOfRuinaFocusOfAttentionPower>(Creature, 1, Creature, null, silent: true);
        SetOpeningMove();
    }

    public override void BeforeRemovedFromRoom()
    {
        StopWarmAmbientLoop();
        base.BeforeRemovedFromRoom();
    }

    // 树木在玩家回合开始时重新生成，早于本回合抽取招式，所以招式按树木已复活来选择。
    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (TurnParticipants.IsRoundPlayerTurn(side))
        {
            await RespawnTreeIfPending();
        }

        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        StopWarmAmbientLoop();
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        MoveState emptyHeart = Register(new MoveState(
            EmptyHeartMoveId,
            EmptyHeartMove,
            new CombinedDefendBuffIntent(
                EmptyHeartBlock,
                "WARMHEARTED_WOODSMAN_EMPTY_HEART.description",
                IntentBadge.FromPower<ThornsPower>(() => EmptyHeartTemporaryThorns))));

        MoveState giantAxeSlash = Register(new MoveState(
            GiantAxeSlashMoveId,
            GiantAxeSlashMove,
            new WarmheartedWoodsmanAttackIntent(
                () => GetGiantAxeSlashDamage(),
                () => GiantAxeSlashHits,
                "WARMHEARTED_WOODSMAN_GIANT_AXE_SLASH.description")));

        MoveState doNotTakeMyHeart = Register(new MoveState(
            DoNotTakeMyHeartMoveId,
            DoNotTakeMyHeartMove,
            new CombinedAttackDefendIntent(
                () => GetDoNotTakeMyHeartDamage(),
                null,
                "WARMHEARTED_WOODSMAN_DO_NOT_TAKE_MY_HEART.description",
                DoNotTakeMyHeartBlock)));

        MoveState warmHeartbeat = Register(new MoveState(
            WarmHeartbeatMoveId,
            WarmHeartbeatMove,
            new CombinedAttackBuffIntent(
                () => GetWarmHeartbeatDamage(),
                null,
                "WARMHEARTED_WOODSMAN_WARM_HEARTBEAT.description",
                IntentBadge.FromPower<LibraryEndurancePower>(
                    WarmHeartbeatEndurance,
                    WarmHeartbeatEnduranceTurns.ToString(),
                    WarmHeartbeatEndurance.ToString()))));

        MoveState fierceRumble = Register(new MoveState(
            FierceRumbleMoveId,
            FierceRumbleMove,
            new CombinedAttackDefendIntent(
                () => GetFierceRumbleDamage(),
                null,
                "WARMHEARTED_WOODSMAN_FIERCE_RUMBLE.description",
                FierceRumbleBlock)));

        MoveState logging = Register(new MoveState(
            LoggingMoveId,
            LoggingMove,
            new WarmheartedWoodsmanAttackIntent(
                () => GetLoggingDamage(),
                () => LoggingHits,
                "WARMHEARTED_WOODSMAN_LOGGING.description",
                "WARMHEARTED_WOODSMAN_LOGGING_TREE.description")));

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng));
        emptyHeart.FollowUpState = router;
        giantAxeSlash.FollowUpState = router;
        doNotTakeMyHeart.FollowUpState = router;
        warmHeartbeat.FollowUpState = router;
        fierceRumble.FollowUpState = router;
        logging.FollowUpState = router;

        return new MonsterMoveStateMachine(
            [emptyHeart, giantAxeSlash, doNotTakeMyHeart, warmHeartbeat, fierceRumble, logging, router],
            router);
    }

    // 原版在每个玩家回合开始时抽取本回合的招式，此时 RoundNumber 就是该招式执行的回合。
    internal string ResolvePlannedMoveId(Rng rng)
    {
        if (IsEmptyHeartTurn(GetPlannedTurnNumber()))
        {
            return EmptyHeartMoveId;
        }

        if (rng.NextInt(ChancePercentRange) < GetLoggingChancePercent())
        {
            return LoggingMoveId;
        }

        string[] candidates = RandomMoveIds
            .Where(moveId => moveId != _lastMoveId)
            .ToArray();
        return candidates[rng.NextInt(candidates.Length)];
    }

    internal async Task ApplyViolentHeartTreeKillRewards(PlayerChoiceContext choiceContext)
    {
        if (Creature.IsDead)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(KillSfxPath, -1f);
        decimal heal = Math.Ceiling(Creature.MaxHp * ViolentHeartHealPercent / 100m);
        await CreatureCmd.Heal(Creature, heal);
        await PowerCmdCompat.Apply<StrengthPower>(
            choiceContext,
            Creature,
            ViolentHeartStrength,
            Creature,
            null);
        await AddWarmHeart(choiceContext, ViolentHeartWarmHeartGain);
    }

    internal void NotifyTreeDied()
    {
        if (Creature?.IsAlive != true || _treeReviveCharges <= 0)
        {
            return;
        }

        _treeRespawnPending = true;
    }

    private async Task ApplyInitialWarmHeart()
    {
        WarmHeartPower? warmHeart = await PowerCmdCompat.Apply<WarmHeartPower>(
            Creature,
            WarmHeartPlaceholderStacks,
            Creature,
            null,
            silent: true);
        warmHeart?.SetStacksSilently(0);
    }

    private async Task AddWarmHeart(PlayerChoiceContext choiceContext, int amount)
    {
        if (amount <= 0 || Creature.IsDead)
        {
            return;
        }

        bool hadWarmHeart = HasWarmHeart;
        await PowerCmdCompat.Apply<WarmHeartPower>(choiceContext, Creature, amount, Creature, null, silent: false);
        if (!hadWarmHeart && HasWarmHeart)
        {
            await CreatureCmd.TriggerAnim(Creature, "WarmIdle", 0f);
        }

        RefreshWarmHeartPresentation();
    }

    private async Task EmptyHeartMove(IReadOnlyList<Creature> targets)
    {
        _lastMoveId = EmptyHeartMoveId;
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.25f);
        await CreatureCmd.GainBlock(Creature, EmptyHeartBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<ThornsPower>(Creature, EmptyHeartTemporaryThorns, Creature, null);
        await PowerCmdCompat.Apply<WarmheartedWoodsmanTemporaryThornsPower>(
            Creature,
            EmptyHeartTemporaryThorns,
            Creature,
            null,
            silent: true);
    }

    private async Task GiantAxeSlashMove(IReadOnlyList<Creature> targets)
    {
        _lastMoveId = GiantAxeSlashMoveId;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await DamageCmd.Attack(GetGiantAxeSlashDamage())
            .FromMonster(this)
            .WithHitCount(GiantAxeSlashHits)
            .WithAttackerAnim("AttackSlash", 0.36f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task DoNotTakeMyHeartMove(IReadOnlyList<Creature> targets)
    {
        _lastMoveId = DoNotTakeMyHeartMoveId;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await DamageCmd.Attack(GetDoNotTakeMyHeartDamage())
            .FromMonster(this)
            .WithAttackerAnim("AttackSlash", 0.36f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.15f);
        await CreatureCmd.GainBlock(Creature, DoNotTakeMyHeartBlock, ValueProp.Move, null);
    }

    private async Task WarmHeartbeatMove(IReadOnlyList<Creature> targets)
    {
        _lastMoveId = WarmHeartbeatMoveId;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await DamageCmd.Attack(GetWarmHeartbeatDamage())
            .FromMonster(this)
            .WithAttackerAnim("AttackBlunt", 0.36f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            Creature,
            WarmHeartbeatEndurance,
            WarmHeartbeatEnduranceTurns,
            Creature,
            null);
    }

    private async Task FierceRumbleMove(IReadOnlyList<Creature> targets)
    {
        _lastMoveId = FierceRumbleMoveId;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await DamageCmd.Attack(GetFierceRumbleDamage())
            .FromMonster(this)
            .WithAttackerAnim("AttackBlunt", 0.36f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.15f);
        await CreatureCmd.GainBlock(Creature, FierceRumbleBlock, ValueProp.Move, null);
    }

    // 树木存活时伐木只砍树木；树木在中途倒下后剩余段数落空，不转向玩家。
    private async Task LoggingMove(IReadOnlyList<Creature> targets)
    {
        _lastMoveId = LoggingMoveId;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        using (BeginLoggingTreeTargetScope())
        {
            await DamageCmd.Attack(GetLoggingDamage())
                .FromMonster(this)
                .WithHitCount(LoggingHits)
                .WithAttackerAnim("AttackSlash", 0.28f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }
    }

    private async Task RespawnTreeIfPending()
    {
        if (!_treeRespawnPending)
        {
            return;
        }

        _treeRespawnPending = false;
        if (_treeReviveCharges <= 0 || Creature.IsDead || Creature.CombatState == null)
        {
            return;
        }

        _treeReviveCharges = Math.Max(0, _treeReviveCharges - TreeRespawnChargeCost);
        await CreatureCmd.Add<WoodsmanTree>(Creature.CombatState, WarmheartedWoodsmanStrong.TreeSlot);
    }

    private int GetPlannedTurnNumber()
    {
        return Math.Max(1, Creature.CombatState?.RoundNumber ?? 1);
    }

    private static bool IsEmptyHeartTurn(int turnNumber)
    {
        return (turnNumber - 1) % EmptyHeartCycleTurns == 0;
    }

    private int GetLoggingChancePercent()
    {
        return GetLoggingTreeTarget() != null
            ? LoggingChancePercentWithTree
            : LoggingChancePercentWithoutTree;
    }

    private void RefreshWarmHeartPresentation()
    {
        bool hasHeart = HasWarmHeart;
        if (hasHeart && _warmAmbientLoop == null)
        {
            _warmAmbientLoop = LocalOggLoopPlayer.StartLoop(WarmAmbientSfxPath, -7f);
        }
        else if (!hasHeart)
        {
            StopWarmAmbientLoop();
        }
    }

    private void StopWarmAmbientLoop()
    {
        _warmAmbientLoop?.Stop();
        _warmAmbientLoop = null;
    }

    private void SetOpeningMove()
    {
        if (!IsMutable || MoveStateMachine == null || _statesById.Count == 0)
        {
            return;
        }

        if (_statesById.TryGetValue(EmptyHeartMoveId, out MoveState? state))
        {
            SetMoveImmediate(state, forceTransition: true);
        }
    }

    public bool UsesTargetedAttackContract(Creature owner)
    {
        return owner == Creature
            && NextMove.Id == LoggingMoveId
            && GetLoggingTreeTarget() != null;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        Creature? tree = GetLoggingTreeTarget();
        return tree == null ? [] : [tree];
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        return GetLoggingTreeTarget()?.Name ?? string.Empty;
    }

    private Creature? GetLoggingTreeTarget()
    {
        return Creature.CombatState == null
            ? null
            : WarmheartedWoodsmanEncounterHelper.FindTree(Creature.CombatState);
    }

    // 树木与樵夫同属敌方阵营：FromMonster 会把目标重置为玩家，需在作用域内强制改打树木。
    private IDisposable? BeginLoggingTreeTargetScope()
    {
        Creature? tree = GetLoggingTreeTarget();
        return tree == null
            ? null
            : TargetedMonsterAttackHelper.ForceTargets(Creature, [tree]);
    }

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

    private int GetGiantAxeSlashDamage() =>
        GetAscensionDamage(GiantAxeSlashBaseDamage, GiantAxeSlashHighAscensionDamage);

    private int GetDoNotTakeMyHeartDamage() =>
        GetAscensionDamage(DoNotTakeMyHeartBaseDamage, DoNotTakeMyHeartHighAscensionDamage);

    private int GetWarmHeartbeatDamage() =>
        GetAscensionDamage(WarmHeartbeatBaseDamage, WarmHeartbeatHighAscensionDamage);

    private int GetFierceRumbleDamage() =>
        GetAscensionDamage(FierceRumbleBaseDamage, FierceRumbleHighAscensionDamage);

    private int GetLoggingDamage() =>
        GetAscensionDamage(LoggingBaseDamage, LoggingHighAscensionDamage);

    private static int GetAscensionDamage(int baseDamage, int highAscensionDamage) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            highAscensionDamage,
            baseDamage);

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is MoveState move)
            {
                foreach (AbstractIntent intent in move.Intents)
                {
                    yield return intent;
                }
            }
        }
    }

    private void AddPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !IsWarmheartedWoodsmanEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<WarmheartedWoodsmanPageRelic>(room, PageRelicTitleLocKey);
    }

    private static bool IsWarmheartedWoodsmanEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is WarmheartedWoodsman);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            StopWarmAmbientLoop();
            AddPageRewardsFromDeathHook(creature);
        }

        return Task.CompletedTask;
    }
}

public sealed class WoodsmanTree : LorMonsterModel
{
    internal const string HelpMeMoveId = "HELP_ME";
    internal const string IdleTexturePath = WarmheartedWoodsman.TextureRoot + "tree.png";

    private const int BaseMinHp = 52; // 树木：低于 ToughEnemies 进阶时的最低初始生命。
    private const int BaseMaxHp = 56; // 树木：低于 ToughEnemies 进阶时的最高初始生命。
    private const int HighAscensionMinHp = 57; // 树木：达到 ToughEnemies 进阶时的最低初始生命。
    private const int HighAscensionMaxHp = 60; // 树木：达到 ToughEnemies 进阶时的最高初始生命。
    private const int ChaoResistance = 100; // 树木：混乱抗性上限。
    private const int HeartPlaceholderStacks = 1; // 心脏：0 层无法施加，先以此层数挂上再设为剩余次数。

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMinHp, BaseMinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMaxHp, BaseMaxHp);

    public override int DefaultChaoResistance => ChaoResistance;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override IEnumerable<string> AssetPaths =>
        WoodsmanTreeCreatureVisuals.Profile.AssetPaths
            .Concat(
            [
                "res://images/powers/woodsman_tree_heart_passive_power.png"
            ])
            .Concat(
                EnumerateIntentAssets()
                    .SelectMany(static intent => intent.AssetPaths));

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        int revives = WarmheartedWoodsmanEncounterHelper.FindWoodsman(Creature.CombatState)?.Monster is WarmheartedWoodsman woodsman
            ? woodsman.TreeReviveCharges
            : WarmheartedWoodsman.InitialTreeReviveCharges;
        WoodsmanTreeHeartPassivePower? passive = await PowerCmdCompat.Apply<WoodsmanTreeHeartPassivePower>(
            Creature,
            Math.Max(HeartPlaceholderStacks, revives),
            Creature,
            null,
            silent: true);
        passive?.SetRevives(revives);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1, Creature, null, silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState helpMe = new(
            HelpMeMoveId,
            HelpMeMove,
            new HiddenIntent());
        helpMe.FollowUpState = helpMe;
        return new MonsterMoveStateMachine([helpMe], helpMe);
    }

    private Task HelpMeMove(IReadOnlyList<Creature> targets)
    {
        return Task.CompletedTask;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new DefendIntent();
    }

    // 死亡来源不限，只登记重新生成；暴跳之心的击杀奖励由樵夫的能力在造成伤害后判定。
    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            WarmheartedWoodsman? woodsman = Creature.CombatState?.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<WarmheartedWoodsman>()
                .FirstOrDefault(static living => living.Creature.IsAlive);
            woodsman?.NotifyTreeDied();
        }

        return Task.CompletedTask;
    }
}
