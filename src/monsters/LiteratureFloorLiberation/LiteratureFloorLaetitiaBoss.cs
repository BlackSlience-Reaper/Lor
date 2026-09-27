using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.cards.Leticia;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.Leticia;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.powers.LiteratureFloorLiberation;
using LibraryOfRuina.visuals.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.LiteratureFloorLiberation;

public sealed class LiteratureFloorLaetitiaBoss :
    LibraryMonsterModel,
    ILiberationPrimaryPhaseBoss
{
    public const int Phase = 1;
    private const string SendGiftMoveId = "SEND_YOU_A_GIFT";
    private const string DontGetHurtMoveId = "DONT_GET_HURT";
    private const string HaveFunMoveId = "HAVE_FUN";
    private const string ItsAGiftMoveId = "ITS_A_GIFT";
    private const string SuperGiftMoveId = "SUPER_GIFT";
    public const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";

    private const int SendGiftBlock = 18;
    private const int SendGiftCards = 3;
    private const int DontGetHurtBlock = 20;
    private const int DontGetHurtHealPercent = 8;
    private const int HaveFunBlock = 24;
    private const int ItsAGiftBlock = 36;
    private const int DebuffAmount = 3;
    private const int DebuffTurns = 1;
    private const int SuperGiftCards = 2;

    private const string AttackSfxPath =
        "res://audio/sfx/leticia/leticia_attack.ogg";
    private const string GuardSfxPath =
        "res://audio/sfx/leticia/leticia_guard.ogg";
    private const string GiftOpenSfxPath =
        "res://audio/sfx/leticia/gift_open.ogg";
    private const string StrongChargeSfxPath =
        "res://audio/sfx/literature_floor_liberation/laetitia_strong_charge.ogg";
    private const string StrongAttackSfxPath =
        "res://audio/sfx/literature_floor_liberation/laetitia_strong_attack.ogg";

    private const string BackgroundTextScope =
        "literature_floor_laetitia_phase_1";
    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea =
        new(150f, 200f, 900f, 450f);
    private static readonly string[] BackgroundTextLineKeys =
    [
        "LITERATURE_FLOOR_LAETITIA_BOSS.backgroundText.0",
        "LITERATURE_FLOOR_LAETITIA_BOSS.backgroundText.1",
        "LITERATURE_FLOOR_LAETITIA_BOSS.backgroundText.2",
        "LITERATURE_FLOOR_LAETITIA_BOSS.backgroundText.3",
        "LITERATURE_FLOOR_LAETITIA_BOSS.backgroundText.4",
        "LITERATURE_FLOOR_LAETITIA_BOSS.backgroundText.5",
        "LITERATURE_FLOOR_LAETITIA_BOSS.backgroundText.6",
        "LITERATURE_FLOOR_LAETITIA_BOSS.backgroundText.7",
        "LITERATURE_FLOOR_LAETITIA_BOSS.backgroundText.8"
    ];

    private MoveState? _superGiftState;
    private MoveState? _reviveAndEmpowerState;
    private bool _backgroundTextStarted;

    public int LiberationPhase => Phase;

    private int AttackDamage => AscensionHelper.GetValueIfAscension(
        AscensionLevel.DeadlyEnemies,
        14,
        12);

    private int PermanentStrong => AscensionHelper.GetValueIfAscension(
        AscensionLevel.DeadlyEnemies,
        3,
        2);

    internal static (int Min, int Max) DebugHpRange(bool toughEnemies) =>
        toughEnemies ? (93, 95) : (80, 87);

    internal static int DebugAttackDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 14 : 12;

    internal static int DebugPermanentStrong(bool deadlyEnemies) =>
        deadlyEnemies ? 3 : 2;

    internal static decimal CalculateHealAmount(decimal maxHp) =>
        Math.Max(
            1m,
            Math.Ceiling(maxHp * DontGetHurtHealPercent / 100m));

    internal static decimal CalculateAdjustedBlock(
        int baseBlock,
        int minimumGiftCount)
    {
        decimal multiplier = Math.Max(
            0m,
            1m - Math.Max(0, minimumGiftCount)
            * LiteratureFloorLaetitiaPlayWithMePassivePower
                .BlockReductionPercentPerGift
            / 100m);
        return Math.Floor(baseBlock * multiplier);
    }

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(
        AscensionLevel.ToughEnemies,
        93,
        80);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(
        AscensionLevel.ToughEnemies,
        95,
        87);

    public override int DefaultChaoResistance => 70;

    public override bool ShouldDisappearFromDoom => false;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => CreateUniformResistance(
            LibraryResistanceLevel.Normal);

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => CreateUniformResistance(
            LibraryResistanceLevel.Endure);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                LiteratureFloorLaetitiaBossCreatureVisuals.ScenePath,
                AttackSfxPath,
                GuardSfxPath,
                GiftOpenSfxPath,
                StrongChargeSfxPath,
                StrongAttackSfxPath,
                LeticiaFilterOverlayController.FilterOneTexturePath,
                "res://images/powers/library_passive_green.png",
                "res://images/powers/history_floor_corrosion_power.png"
            };
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
        _backgroundTextStarted = false;
        if (Creature.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<HistoryFloorCorrosionPower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<
            LiteratureFloorLaetitiaSuperGiftPassivePower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<
            LiteratureFloorLaetitiaPlayWithMePassivePower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<
            LiteratureFloorLaetitiaLonelyPassivePower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        await RefreshLonelyResistance();
    }

    public override void BeforeRemovedFromRoom()
    {
        StopBackgroundText();
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead && !_backgroundTextStarted)
        {
            _backgroundTextStarted = true;
            MoonTextService.StartRandomLoop(
                Creature,
                BackgroundTextScope,
                BackgroundTextLineKeys
                    .Select(L10NMonsterLookup)
                    .ToArray(),
                BackgroundTextIntervalSeconds,
                BackgroundTextSpawnArea);
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
        if (GetEncounter() is { } encounter)
        {
            await encounter.SpawnDueFriends(side, combatState);
        }

        await RefreshLonelyResistance();
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return;
        }

        StopBackgroundText();
        if (GetEncounter() is { } encounter)
        {
            await encounter.OnPhaseBossDeath(
                this,
                wasRemovalPrevented,
                deathAnimLength);
        }
    }

    public Task TriggerReviveAndEmpowerState()
    {
        ForceReviveAndEmpowerState();
        return Task.CompletedTask;
    }

    public void ForceReviveAndEmpowerState()
    {
        if (_reviveAndEmpowerState != null)
        {
            SetMoveImmediate(
                _reviveAndEmpowerState,
                forceTransition: true);
        }
    }

    internal async Task OnAllGiftBoxesDefeated(
        bool queueForImmediateEnemyAction)
    {
        await RefreshLonelyResistance();
        if (!queueForImmediateEnemyAction
            || Creature.IsDead
            || _superGiftState == null)
        {
            return;
        }

        SetMoveImmediate(_superGiftState, forceTransition: true);
        if (NCombatRoom.Instance?.GetCreatureNode(Creature) is { } node)
        {
            await node.RefreshIntents();
        }
    }

    internal async Task RefreshLonelyResistance()
    {
        if (Creature.IsDead || Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        bool hasLivingFriend = Creature.CombatState?.Enemies.Any(enemy =>
            enemy != Creature && enemy.IsAlive) == true;
        LibraryResistanceLevel targetLevel = hasLivingFriend
            ? LibraryResistanceLevel.Normal
            : LibraryResistanceLevel.Fatal;
        if (libraryCreature.GetPhysicalResistanceLevel(
                LibraryDamageType.Slash) == targetLevel
            && libraryCreature.GetPhysicalResistanceLevel(
                LibraryDamageType.Pierce) == targetLevel
            && libraryCreature.GetPhysicalResistanceLevel(
                LibraryDamageType.Blunt) == targetLevel)
        {
            return;
        }

        var context = new ThrowingPlayerChoiceContext();
        await LibraryCreatureCmd.SetPhysicalResistance(
            context,
            libraryCreature,
            Creature,
            LibraryDamageType.Slash,
            targetLevel);
        await LibraryCreatureCmd.SetPhysicalResistance(
            context,
            libraryCreature,
            Creature,
            LibraryDamageType.Pierce,
            targetLevel);
        await LibraryCreatureCmd.SetPhysicalResistance(
            context,
            libraryCreature,
            Creature,
            LibraryDamageType.Blunt,
            targetLevel);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var sendGift = new MoveState(
            SendGiftMoveId,
            SendGiftMove,
            new SingleAttackIntent(AttackDamage),
            new DetailedStatusCardIntent<LeticiaGift>(
                SendGiftCards,
                PileType.Hand,
                DetailedIntentScopeText.Target),
            new DefendIntent());
        var dontGetHurt = new MoveState(
            DontGetHurtMoveId,
            DontGetHurtMove,
            new DefendIntent(),
            new HealIntent());
        var haveFun = new MoveState(
            HaveFunMoveId,
            HaveFunMove,
            new CombinedDefendBuffIntent(HaveFunBlock, 
                null, 
                IntentBadge.FromPower<LibraryStrongPower>(
                    PermanentStrong, 
                    null, 
                    PermanentStrong.ToString())));
        var itsAGift = new MoveState(
            ItsAGiftMoveId,
            ItsAGiftMove,
            new DefendIntent(),
            new DebuffIntent(strong: true));
        var superGiftIntent = new IndiscriminateAttackIntent(
            () => AttackDamage,
            () => 1,
            null);
        _superGiftState = new MoveState(
            SuperGiftMoveId,
            superGiftIntent.WithPreAttackBlockBreak(this, SuperGiftMove),
            superGiftIntent,
            new DetailedStatusCardIntent<LeticiaGift>(
                SuperGiftCards,
                PileType.Discard,
                DetailedIntentScopeText.Target));
        _reviveAndEmpowerState = new LibraryPhaseTransitionMoveState(
            ReviveAndEmpowerMoveId,
            ReviveAndEmpowerMove,
            new HealIntent(),
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        var afterSend = CreateFollowUpBranch(
            "AFTER_SEND",
            _superGiftState,
            dontGetHurt,
            itsAGift);
        var afterDontGetHurt = CreateFollowUpBranch(
            "AFTER_DONT_GET_HURT",
            _superGiftState,
            haveFun,
            itsAGift);
        var afterHaveFun = CreateFollowUpBranch(
            "AFTER_HAVE_FUN",
            _superGiftState,
            sendGift,
            itsAGift);
        var afterItsAGift = new ConditionalBranchState(
            "AFTER_ITS_A_GIFT");
        afterItsAGift.AddState(
            _superGiftState,
            ShouldUseSuperGift);
        afterItsAGift.AddState(itsAGift, () => true);

        sendGift.FollowUpState = afterSend;
        dontGetHurt.FollowUpState = afterDontGetHurt;
        haveFun.FollowUpState = afterHaveFun;
        itsAGift.FollowUpState = afterItsAGift;
        _superGiftState.FollowUpState = itsAGift;
        _reviveAndEmpowerState.FollowUpState = sendGift;

        return new MonsterMoveStateMachine(
            [
                sendGift,
                dontGetHurt,
                haveFun,
                itsAGift,
                _superGiftState,
                _reviveAndEmpowerState,
                afterSend,
                afterDontGetHurt,
                afterHaveFun,
                afterItsAGift
            ],
            sendGift);
    }

    private async Task ReviveAndEmpowerMove(
        IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        await PlayActionAnimationToCompletion("Cast");
        if (Creature.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseTransition(this);
        }
    }

    private ConditionalBranchState CreateFollowUpBranch(
        string id,
        MoveState superGift,
        MoveState whileBoxesLive,
        MoveState normalAfterBoxes)
    {
        var branch = new ConditionalBranchState(id);
        branch.AddState(superGift, ShouldUseSuperGift);
        branch.AddState(whileBoxesLive, HasLivingGiftBoxes);
        branch.AddState(normalAfterBoxes, () => true);
        return branch;
    }

    private async Task SendGiftMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await PlayActionAnimationToCompletion("Attack");
        await DamageCmd.Attack(AttackDamage)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        await AddGiftsToPlayers(SendGiftCards, PileType.Hand);
        await GainAdjustedBlock([Creature], SendGiftBlock);
    }

    private async Task DontGetHurtMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(GuardSfxPath, -2f);
        await PlayActionAnimationToCompletion("Cast");
        Creature[] enemies = LivingEnemies().ToArray();
        await GainAdjustedBlock(enemies, DontGetHurtBlock);
        foreach (Creature enemy in enemies)
        {
            await CreatureCmd.Heal(
                enemy,
                CalculateHealAmount(enemy.MaxHp));
        }
    }

    private async Task HaveFunMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(GuardSfxPath, -2f);
        await PlayActionAnimationToCompletion("Cast");
        Creature[] enemies = LivingEnemies().ToArray();
        foreach (Creature enemy in enemies)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                enemy,
                PermanentStrong,
                turns: -1,
                Creature,
                null);
        }

        await GainAdjustedBlock(enemies, HaveFunBlock);
    }

    private async Task ItsAGiftMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(GiftOpenSfxPath, -2f);
        LeticiaFilterOverlayController.PlayGiftOpenOverlay();
        await PlayActionAnimationToCompletion("Cast");
        await GainAdjustedBlock([Creature], ItsAGiftBlock);
        foreach (Creature player in LivingPlayers())
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                player,
                DebuffAmount,
                DebuffTurns,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                player,
                DebuffAmount,
                DebuffTurns,
                Creature,
                null);
        }

        GetEncounter()?.CompleteNormalGiftMove();
    }

    private async Task SuperGiftMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(StrongChargeSfxPath, -1f);
        LeticiaFilterOverlayController.PlayGiftOpenOverlay();
        await PlayActionAnimationToCompletion("SuperGift");
        LocalOggOneShotPlayer.Play(StrongAttackSfxPath, -1f);
        await DamageCmd.Attack(AttackDamage)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await AddGiftsToPlayers(SuperGiftCards, PileType.Discard);
        GetEncounter()?.CompleteSuperGift();
    }

    private async Task PlayActionAnimationToCompletion(string triggerName)
    {
        await CreatureCmd.TriggerAnim(Creature, triggerName, 0f);
        await Cmd.Wait(
            LiteratureFloorLaetitiaAnimationContract.DurationForTrigger(
                triggerName));
    }

    private async Task AddGiftsToPlayers(int count, PileType pileType)
    {
        Creature[] players = LivingPlayers().ToArray();
        if (players.Length == 0)
        {
            return;
        }

        LeticiaFilterOverlayController.PlayGiftOpenOverlay();
        await CardPileCmdCompat.AddToCombatAndPreview<LeticiaGift>(
            players,
            pileType,
            count,
            addedByPlayer: false,
            CardPilePosition.Random);
    }

    private async Task GainAdjustedBlock(
        IEnumerable<Creature> targets,
        int baseBlock)
    {
        int giftCount = LiteratureFloorGiftHandMetrics.MinGiftCount(
            Creature.CombatState);
        decimal adjustedBlock = CalculateAdjustedBlock(
            baseBlock,
            giftCount);
        foreach (Creature target in targets.Where(static target =>
                     target.IsAlive))
        {
            await CreatureCmd.GainBlock(
                target,
                adjustedBlock,
                ValueProp.Move,
                null);
        }
    }

    private bool HasLivingGiftBoxes() =>
        GetEncounter()?.HasLivingGiftBoxes(Creature.CombatState) == true;

    private bool ShouldUseSuperGift() =>
        GetEncounter()?.IsSuperGiftDue(Creature.CombatState) == true;

    private LiteratureFloorLiberationEncounter? GetEncounter() =>
        Creature.CombatState?.Encounter
            as LiteratureFloorLiberationEncounter;

    private IEnumerable<Creature> LivingPlayers() =>
        Creature.CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray() ?? [];

    private IEnumerable<Creature> LivingEnemies() =>
        Creature.CombatState?.Enemies
            .Where(static enemy => enemy.IsAlive)
            .ToArray() ?? [];

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MoveState move in stateMachine.States.Values
                     .OfType<MoveState>())
        {
            foreach (AbstractIntent intent in move.Intents)
            {
                yield return intent;
            }
        }
    }

    private void StopBackgroundText()
    {
        if (_backgroundTextStarted)
        {
            MoonTextService.StopRandomLoop(Creature, BackgroundTextScope);
            _backgroundTextStarted = false;
        }
    }

    private static LibraryCreatureResistanceData.Resistance
        CreateUniformResistance(LibraryResistanceLevel level) =>
        new()
        {
            Slash = level,
            Pierce = level,
            Blunt = level
        };
}
