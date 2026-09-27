using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryLib.Hooks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.WarmheartedWoodsman;
using LibraryOfRuina.intents;
using LibraryOfRuina.intents.WarmheartedWoodsman;
using LibraryOfRuina.powers.LittleRedMercenary;
using LibraryOfRuina.powers.WarmheartedWoodsman;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.WarmheartedWoodsman;
using LibraryOfRuina.visuals.WarmheartedWoodsman;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.WarmheartedWoodsman;

public sealed class WarmheartedWoodsman : LibraryMonsterModel, ITargetedMonsterAttackProvider
{
    internal const string EmptyHeartMoveId = "EMPTY_HEART";
    internal const string GiantAxeSlashMoveId = "GIANT_AXE_SLASH";
    internal const string DoNotTakeMyHeartMoveId = "DO_NOT_TAKE_MY_HEART";
    internal const string WarmHeartbeatMoveId = "WARM_HEARTBEAT";
    internal const string FierceRumbleMoveId = "FIERCE_RUMBLE";
    internal const string LoggingMoveId = "LOGGING";
    private const string RouterStateId = "WARMHEARTED_WOODSMAN_ROUTER";

    public const int VerdantForestEnergy = 1;
    public const int WantAHeartBuffPerEnergy = 1;
    public const int ViolentHeartHealPercent = 10;
    public const int ViolentHeartStrong = 4;
    public const int OneTurnBuffDuration = 1;
    public const int InitialTreeReviveCharges = 2;

    internal const int EmptyHeartBlock = 99;
    internal const int EmptyHeartTemporaryThorns = 20;
    internal const int GiantAxeSlashMinDamage = 16;
    internal const int GiantAxeSlashMaxDamage = 18;
    internal const int GiantAxeSlashHits = 2;
    internal const int DoNotTakeMyHeartMinDamage = 27;
    internal const int DoNotTakeMyHeartMaxDamage = 33;
    internal const int DoNotTakeMyHeartBlock = 40;
    internal const int WarmHeartbeatMinDamage = 23;
    internal const int WarmHeartbeatMaxDamage = 24;
    internal const int FierceRumbleMinDamage = 24;
    internal const int FierceRumbleMaxDamage = 29;
    internal const int FierceRumbleBlock = 43;
    internal const int LoggingSetupMinDamage = 8;
    internal const int LoggingSetupMaxDamage = 12;
    internal const int LoggingSetupHits = 3;
    internal const int LoggingFinalMinDamage = 29;
    internal const int LoggingFinalMaxDamage = 34;
    internal const int LoggingFinalDamageLossPerFullBlock = 20;

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
        "res://images/powers/warmhearted_woodsman_verdant_forest_passive_power.png",
        "res://images/powers/warmhearted_woodsman_want_a_heart_passive_power.png",
        "res://images/powers/warmhearted_woodsman_violent_heart_passive_power.png",
        "res://images/powers/warmhearted_woodsman_empty_heart_passive_power.png",
        "res://images/powers/warmhearted_woodsman_temporary_thorns_power.png"
    ];

    private static readonly string PageRelicTitleLocKey = $"{ModelDb.GetId<WarmheartedWoodsmanPageRelic>().Entry}.title";
    private static readonly string[] RandomAttackMoveIds =
    [
        GiantAxeSlashMoveId,
        DoNotTakeMyHeartMoveId,
        WarmHeartbeatMoveId,
        FierceRumbleMoveId
    ];

    private Dictionary<string, MoveState> _statesById = [];
    private int _attackFormMoveCount;
    private string? _lastRandomAttackMoveId;
    private int _treeReviveCharges = InitialTreeReviveCharges;
    private bool _treeRespawnPending;
    private bool _targetTreeAsAttackTarget;
    private bool _treeTargetAttackInProgress;
    private bool _emptyHeartCanTrigger;
    private bool _emptyHeartChaoStunPending;
    private LocalOggLoopPlayer.LoopHandle? _warmAmbientLoop;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
        _warmAmbientLoop = null;
    }

    public bool HasWarmHeart => Creature?.GetPowerAmount<WarmHeartPower>() > 0;

    public int WarmHeartStacks => Creature?.GetPowerAmount<WarmHeartPower>() ?? 0;

    public int TreeReviveCharges => _treeReviveCharges;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 667, 560);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 670, 565);

    public override int DefaultChaoResistance => 230;

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
        _attackFormMoveCount = 0;
        _lastRandomAttackMoveId = null;
        _treeReviveCharges = InitialTreeReviveCharges;
        _treeRespawnPending = false;
        _targetTreeAsAttackTarget = false;
        _treeTargetAttackInProgress = false;
        _emptyHeartCanTrigger = false;
        _emptyHeartChaoStunPending = false;
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<WarmheartedWoodsmanVerdantForestPassivePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<WarmheartedWoodsmanWantAHeartPassivePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<WarmheartedWoodsmanViolentHeartPassivePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<WarmheartedWoodsmanEmptyHeartPassivePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LibraryOfRuinaFocusOfAttentionPower>(Creature, 1, Creature, null, silent: true);
        ForceRefreshMoveState();
    }

    public override void BeforeRemovedFromRoom()
    {
        StopWarmAmbientLoop();
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            await ResolvePendingEmptyHeartChaoStun();
            await RespawnTreeIfPending(choiceContext);
            RefreshTreeAttackTargeting();
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
            new TargetedMonsterAttackIntent(
                () => GetGiantAxeSlashDamage(),
                () => GiantAxeSlashHits,
                "WARMHEARTED_WOODSMAN_GIANT_AXE_SLASH.description")));

        MoveState doNotTakeMyHeart = Register(new MoveState(
            DoNotTakeMyHeartMoveId,
            DoNotTakeMyHeartMove,
            new CombinedTargetedAttackDefendIntent(
                () => GetDoNotTakeMyHeartDamage(),
                null,
                "WARMHEARTED_WOODSMAN_DO_NOT_TAKE_MY_HEART.description",
                null,
                false,
                DoNotTakeMyHeartBlock,
                IntentBadge.FromPower<WarmHeartPower>(() => 1))));

        MoveState warmHeartbeat = Register(new MoveState(
            WarmHeartbeatMoveId,
            WarmHeartbeatMove,
            new CombinedTargetedAttackBuffIntent(
                () => GetWarmHeartbeatDamage(),
                null,
                "WARMHEARTED_WOODSMAN_WARM_HEARTBEAT.description",
                null,
                false,
                IntentBadge.FromPower<WarmHeartPower>(() => -1))));

        MoveState fierceRumble = Register(new MoveState(
            FierceRumbleMoveId,
            FierceRumbleMove,
            new CombinedTargetedAttackDefendIntent(
                () => GetFierceRumbleDamage(),
                null,
                "WARMHEARTED_WOODSMAN_FIERCE_RUMBLE.description",
                null,
                false,
                FierceRumbleBlock,
                IntentBadge.FromPower<WarmHeartPower>(() => 1))));

        MoveState logging = Register(new MoveState(
            LoggingMoveId,
            LoggingMove,
            new DynamicAttackIntent(
                () => GetLoggingSetupDamage(),
                () => LoggingSetupHits),
            new WarmheartedWoodsmanLoggingFinalIntent(
                () => GetLoggingSetupDamage(),
                () => GetLoggingFinalDamage(),
                "WARMHEARTED_WOODSMAN_LOGGING_FINAL.description")));

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

    internal string ResolvePlannedMoveId(Rng rng)
    {
        if (!HasWarmHeart)
        {
            EnsureEmptyHeartRolls();
            return EmptyHeartMoveId;
        }

        int nextAttackFormMove = _attackFormMoveCount + 1;
        if (nextAttackFormMove % 3 == 0)
        {
            return LoggingMoveId;
        }

        string[] filtered = RandomAttackMoveIds
            .Where(moveId => moveId != _lastRandomAttackMoveId)
            .ToArray();
        string selected = rng.NextItem(filtered.Length > 0 ? filtered : RandomAttackMoveIds) ?? GiantAxeSlashMoveId;
        return selected;
    }

    internal async Task AddWarmHeart(PlayerChoiceContext choiceContext, int amount)
    {
        if (amount <= 0 || Creature.IsDead)
        {
            return;
        }

        int before = WarmHeartStacks;
        await PowerCmdCompat.Apply<WarmHeartPower>(choiceContext, Creature, amount, Creature, null, silent: false);
        if (WarmHeartStacks > 0)
        {
            _emptyHeartCanTrigger = true;
            if (before <= 0)
            {
                await CreatureCmd.TriggerAnim(Creature, "WarmIdle", 0f);
            }
        }

        RefreshWarmHeartPresentation();
        ForceRefreshMoveState();
    }

    internal async Task RemoveWarmHeart(PlayerChoiceContext choiceContext, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        WarmHeartPower? power = Creature.GetPower<WarmHeartPower>();
        if (power == null)
        {
            return;
        }

        int before = WarmHeartStacks;
        int delta = -Math.Min(amount, power.Amount);
        int remaining = await PowerCmdCompat.ModifyAmount(choiceContext, power, delta, Creature, null, silent: false);
        if (remaining <= 0) // 层数为0不移除。
        {
            _attackFormMoveCount = 0;
            _lastRandomAttackMoveId = null;
            await CreatureCmd.TriggerAnim(Creature, "EmptyIdle", 0f);
            if (before > 0)
            {
                QueueEmptyHeartChaoStun();
            }
        }

        RefreshWarmHeartPresentation();
        ForceRefreshMoveState();
    }

    internal void NotifyTreeDied()
    {
        if (Creature?.IsAlive != true)
        {
            return;
        }

        _targetTreeAsAttackTarget = false;
        if (_treeReviveCharges <= 0)
        {
            return;
        }

        _treeRespawnPending = true;
    }

    internal static Task ApplyWantAHeartFromEnergyGain(Player player, int energyGained)
    {
        if (energyGained <= 0
            || player?.Creature?.CombatState is not CombatState combatState
            || !CombatManager.Instance.IsInProgress)
        {
            return Task.CompletedTask;
        }

        return ApplyWantAHeartFromEnergyGainInternal(combatState, energyGained);
    }

    private static async Task ApplyWantAHeartFromEnergyGainInternal(CombatState combatState, int energyGained)
    {
        foreach (WarmheartedWoodsman woodsman in combatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<WarmheartedWoodsman>())
        {
            if (!woodsman.Creature.HasPower<WarmheartedWoodsmanWantAHeartPassivePower>())
            {
                continue;
            }

            await ApplyOneTurnStackingBuff<LibraryStrongPower>(
                woodsman.Creature,
                energyGained * WantAHeartBuffPerEnergy);
            await ApplyOneTurnStackingBuff<LibraryEndurancePower>(
                woodsman.Creature,
                energyGained * WantAHeartBuffPerEnergy);
        }
    }

    private static async Task ApplyOneTurnStackingBuff<TPower>(Creature target, int amount)
        where TPower : LibraryTurnsPowerModel
    {
        if (amount <= 0)
        {
            return;
        }

        TPower? existing = target.GetPowerInstances<TPower>()
            .FirstOrDefault(static power => power.TurnsRemaining > 0);
        if (existing == null)
        {
            await LibraryPowerCmd.Apply<TPower>(
                new ThrowingPlayerChoiceContext(),
                target,
                amount,
                0,
                IsPermanent: false,
                target,
                null);
            return;
        }

        await LibraryPowerCmd.ModifyAmount(
            new ThrowingPlayerChoiceContext(),
            existing,
            amount,
            0,
            IsPermanent: false,
            target,
            null,
            silent: false);
    }

    private async Task EmptyHeartMove(IReadOnlyList<Creature> targets)
    {
        _attackFormMoveCount = 0;
        _lastRandomAttackMoveId = null;
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.25f);
        await CreatureCmd.GainBlock(Creature, EmptyHeartBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<ThornsPower>(Creature, EmptyHeartTemporaryThorns, Creature, null);
        await PowerCmdCompat.Apply<WarmheartedWoodsmanTemporaryThornsPower>(
            Creature,
            EmptyHeartTemporaryThorns,
            Creature,
            null,
            silent: true);
        await AddWarmHeart(new ThrowingPlayerChoiceContext(), 2);
        ForceRefreshMoveState();
    }

    private async Task GiantAxeSlashMove(IReadOnlyList<Creature> targets)
    {
        _attackFormMoveCount++;
        _lastRandomAttackMoveId = GiantAxeSlashMoveId;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        using (BeginTreeTargetAttackScope())
        {
            await DamageCmd.Attack(GetGiantAxeSlashDamage())
                .FromMonster(this)
                .WithHitCount(GiantAxeSlashHits)
                .WithAttackerAnim("AttackSlash", 0.36f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }
    }

    private async Task DoNotTakeMyHeartMove(IReadOnlyList<Creature> targets)
    {
        _attackFormMoveCount++;
        _lastRandomAttackMoveId = DoNotTakeMyHeartMoveId;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        using (BeginTreeTargetAttackScope())
        {
            await DamageCmd.Attack(GetDoNotTakeMyHeartDamage())
                .FromMonster(this)
                .WithAttackerAnim("AttackSlash", 0.36f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.15f);
        await CreatureCmd.GainBlock(Creature, DoNotTakeMyHeartBlock, ValueProp.Move, null);
        await AddWarmHeart(new ThrowingPlayerChoiceContext(), 1);
    }

    private async Task WarmHeartbeatMove(IReadOnlyList<Creature> targets)
    {
        _attackFormMoveCount++;
        _lastRandomAttackMoveId = WarmHeartbeatMoveId;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        IReadOnlyList<DamageResult> results;
        using (BeginTreeTargetAttackScope())
        {
            results = AttackCommandCompat.Results(await DamageCmd.Attack(GetWarmHeartbeatDamage())
                .FromMonster(this)
                .WithAttackerAnim("AttackBlunt", 0.36f)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null));
        }

        if (results.Any(static result => result.UnblockedDamage > 0))
        {
            await RemoveWarmHeart(new ThrowingPlayerChoiceContext(), 1);
        }
    }

    private async Task FierceRumbleMove(IReadOnlyList<Creature> targets)
    {
        _attackFormMoveCount++;
        _lastRandomAttackMoveId = FierceRumbleMoveId;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        IReadOnlyList<DamageResult> results;
        using (BeginTreeTargetAttackScope())
        {
            results = AttackCommandCompat.Results(await DamageCmd.Attack(GetFierceRumbleDamage())
                .FromMonster(this)
                .WithAttackerAnim("AttackBlunt", 0.36f)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null));
        }

        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.15f);
        await CreatureCmd.GainBlock(Creature, FierceRumbleBlock, ValueProp.Move, null);
        await AddWarmHeart(new ThrowingPlayerChoiceContext(), 1);
    }

    private async Task LoggingMove(IReadOnlyList<Creature> targets)
    {
        _attackFormMoveCount++;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        IReadOnlyList<DamageResult> setupResults = AttackCommandCompat.Results(
            await DamageCmd.Attack(GetLoggingSetupDamage())
                .FromMonster(this)
                .WithHitCount(LoggingSetupHits)
                .WithAttackerAnim("AttackSlash", 0.28f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null));

        int fullyBlocked = setupResults.Count(static result => result.WasFullyBlocked);
        int finalDamage = Math.Max(
            0,
            GetLoggingFinalDamage() - fullyBlocked * LoggingFinalDamageLossPerFullBlock);

        if (finalDamage > 0 && !Creature.IsDead)
        {
            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            await DamageCmd.Attack(finalDamage)
                .FromMonster(this)
                .WithAttackerAnim("LoggingFinal", 0.44f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        if (!Creature.IsDead)
        {
            await RemoveWarmHeart(new ThrowingPlayerChoiceContext(), WarmHeartStacks);
        }
    }

    private async Task<bool> RespawnTreeIfPending(PlayerChoiceContext choiceContext)
    {
        if (!_treeRespawnPending || _treeReviveCharges <= 0 || Creature.IsDead || Creature.CombatState == null)
        {
            _treeRespawnPending = false;
            return false;
        }

        _treeRespawnPending = false;
        _treeReviveCharges--;
        await CreatureCmd.Add<WoodsmanTree>(Creature.CombatState, WarmheartedWoodsmanStrong.TreeSlot);
        return true;
    }

    private void RefreshTreeAttackTargeting()
    {
        Creature? tree = WarmheartedWoodsmanEncounterHelper.FindTree(Creature.CombatState);
        if (tree == null)
        {
            _targetTreeAsAttackTarget = false;
            return;
        }

        _targetTreeAsAttackTarget = HasWarmHeart;
    }

    private void QueueEmptyHeartChaoStun()
    {
        if (!_emptyHeartCanTrigger)
        {
            return;
        }

        _emptyHeartCanTrigger = false;
        _emptyHeartChaoStunPending = true;
    }

    private async Task ResolvePendingEmptyHeartChaoStun()
    {
        if (!_emptyHeartChaoStunPending)
        {
            return;
        }

        _emptyHeartChaoStunPending = false;
        if (Creature.IsDead || Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        ICombatState? combatState = libraryCreature.CombatState;
        if (combatState == null)
        {
            return;
        }

        decimal previousChaoValue = libraryCreature.CurrentChaoValue;
        libraryCreature.SetCurrentChaoValueInternal(0m);
        decimal changedAmount = libraryCreature.CurrentChaoValue - previousChaoValue;
        if (changedAmount != 0m)
        {
            await LibraryHooks.AfterCurrentChaoValueChanged(
                libraryCreature.Player?.RunState ?? combatState.RunState,
                combatState,
                libraryCreature,
                changedAmount,
                LibraryDamageType.None);
        }

        if (libraryCreature.CurrentChaoValue == 0 && !libraryCreature.IsChaoed && libraryCreature.MaxChaoValue != 0)
        {
            await LibraryCreatureCmd.Stun(libraryCreature, EmptyHeartMoveId);
        }
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

    private void ForceRefreshMoveState()
    {
        if (!IsMutable || MoveStateMachine == null || _statesById.Count == 0)
        {
            return;
        }

        string moveId = ResolvePlannedMoveId(RunRng.MonsterAi);
        if (_statesById.TryGetValue(moveId, out MoveState? state))
        {
            SetMoveImmediate(state, forceTransition: true);
        }
    }

    public bool UsesTargetedAttackContract(Creature owner)
    {
        return owner == Creature
            && IsAttackMoveId(NextMove.Id)
            && (_treeTargetAttackInProgress || GetCurrentAttackTarget() != null);
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        Creature? target = GetCurrentAttackTarget();
        return target == null ? [] : [target];
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        return GetCurrentAttackTarget()?.Name ?? string.Empty;
    }

    private Creature? GetCurrentAttackTarget()
    {
        return NextMove.Id == LoggingMoveId
            ? null
            : GetTreeAttackTarget();
    }

    private Creature? GetTreeAttackTarget()
    {
        if (!_targetTreeAsAttackTarget || Creature.CombatState == null)
        {
            return null;
        }

        return WarmheartedWoodsmanEncounterHelper.FindTree(Creature.CombatState);
    }

    private static bool IsAttackMoveId(string moveId)
    {
        return moveId is GiantAxeSlashMoveId
            or DoNotTakeMyHeartMoveId
            or WarmHeartbeatMoveId
            or FierceRumbleMoveId
            or LoggingMoveId;
    }

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

    private void EnsureEmptyHeartRolls()
    {
    }

    private int GetGiantAxeSlashDamage() =>
        GetAscensionDamage(GiantAxeSlashMinDamage, GiantAxeSlashMaxDamage);

    private int GetDoNotTakeMyHeartDamage() =>
        GetAscensionDamage(DoNotTakeMyHeartMinDamage, DoNotTakeMyHeartMaxDamage);

    private int GetWarmHeartbeatDamage() =>
        GetAscensionDamage(WarmHeartbeatMinDamage, WarmHeartbeatMaxDamage);

    private int GetFierceRumbleDamage() =>
        GetAscensionDamage(FierceRumbleMinDamage, FierceRumbleMaxDamage);

    private int GetLoggingSetupDamage() =>
        GetAscensionDamage(LoggingSetupMinDamage, LoggingSetupMaxDamage);

    private int GetLoggingFinalDamage() =>
        GetAscensionDamage(LoggingFinalMinDamage, LoggingFinalMaxDamage);

    private static int GetAscensionDamage(int lowAscensionDamage, int highAscensionDamage) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            highAscensionDamage,
            lowAscensionDamage);

    private IDisposable? BeginTreeTargetAttackScope()
    {
        if (!_targetTreeAsAttackTarget || NextMove.Id == LoggingMoveId)
        {
            return null;
        }

        _treeTargetAttackInProgress = true;
        return new TreeTargetAttackScope(this);
    }

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

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<WarmheartedWoodsmanPageRelic>(
                room,
                player,
                PageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<WarmheartedWoodsmanPageRelic>().ToMutable(), player));
        }
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

    private sealed class TreeTargetAttackScope : IDisposable
    {
        private readonly WarmheartedWoodsman _owner;
        private bool _disposed;

        public TreeTargetAttackScope(WarmheartedWoodsman owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner._treeTargetAttackInProgress = false;
        }
    }

}

public sealed class WoodsmanTree : LibraryMonsterModel
{
    internal const string HelpMeMoveId = "HELP_ME";
    internal const int HelpMeBlock = 6;
    internal const string IdleTexturePath = WarmheartedWoodsman.TextureRoot + "tree.png";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 57, 52);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 60, 56);

    public override int DefaultChaoResistance => 100;

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
            Math.Max(1, revives),
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

    private async Task HelpMeMove(IReadOnlyList<Creature> targets)
    {
        //await CreatureCmd.TriggerAnim(Creature, "Guard", 0.2f);
        //await CreatureCmd.GainBlock(Creature, HelpMeBlock, ValueProp.Move, null);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new DefendIntent();
    }
    

    public override async Task AfterDeath(
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
                .FirstOrDefault(static w => w.Creature.IsAlive);

            if (woodsman != null)
            {
                woodsman.NotifyTreeDied();

                if (!woodsman.Creature.IsDead)
                {
                    LocalOggOneShotPlayer.Play(WarmheartedWoodsman.KillSfxPath, -1f);
                    decimal heal = Math.Ceiling(woodsman.Creature.MaxHp * WarmheartedWoodsman.ViolentHeartHealPercent / 100m);
                    await CreatureCmd.Heal(woodsman.Creature, heal);
                    await PowerCmdCompat.Apply<StrengthPower>(
                        choiceContext,
                        woodsman.Creature,
                        WarmheartedWoodsman.ViolentHeartStrong,
                        woodsman.Creature,
                        null);
                }
            }
        }
    }
}
