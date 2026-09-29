using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.HistoryFloorLiberation;
using LibraryOfRuina.cards;
using LibraryOfRuina.cards.HistoryFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.visuals.HistoryFloorLiberation;
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
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.HistoryFloorLiberation;

public sealed class HistoryFloorWaspBoss : LiberationPhaseBossMonster
{
    private const int Phase = 4;

    public override int DefaultChaoResistance => 60;

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    private const string GloriousBrandMoveId = "GLORIOUS_BRAND";
    private const string LoyaltyEnhancementMoveId = "LOYALTY_ENHANCEMENT";
    private const string WarlikeEnhancementMoveId = "WARLIKE_ENHANCEMENT";
    private const string ForTheKingdomMoveId = "FOR_THE_KINGDOM";
    private const string PunishmentStrikeMoveId = "PUNISHMENT_STRIKE";

    private const int LoyaltyBlock = 8;
    private const int LoyaltyGuard = 1;
    private const int LoyaltyGuardTurns = 2;
    private const int WarlikeBlock = 11;
    private const int WarlikeStrength = 2;
    private const int WarlikeWeak = 2;
    private const float SegmentDelaySeconds = 0.78f;
    private const float BackgroundTextIntervalSeconds = 5f;

    internal const string BackgroundTextScope = "history_floor_liberation_phase_4";

    public const string Root = "res://images/monsters/history_floor/wasp/";
    public const string IdleTexturePath = Root + "wasp_idle.png";
    public const string AttackStrikeTexturePath = Root + "wasp_attack_strike.png";
    public const string AttackPierceTexturePath = Root + "wasp_attack_pierce.png";
    public const string HitTexturePath = Root + "wasp_hit.png";
    public const string EffectTexturePath = Root + "wasp_effect.png";
    public const string LoyaltyTexturePath = Root + "wasp_loyalty.png";

    public const string AttackBuffSfxPath = "res://audio/sfx/history_floor/wasp/wasp_attack_buff.ogg";

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 190f, 980f, 470f);
    private static readonly string[] BackgroundTextLineKeys =
    [
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.0",
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.1",
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.2",
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.3",
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.4",
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.5",
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.6",
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.7",
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.8",
        "HISTORY_FLOOR_WASP_BOSS.backgroundText.normal.9"
    ];

    public static readonly string[] OverlayAssetPaths =
    [
        HistoryFloorLiberationBackgroundController.WaspBuffOverlayTexturePath,
        HistoryFloorLiberationBackgroundController.WaspLoyaltyOverlayTexturePath
    ];

    private static readonly string[] SfxAssetPaths =
    [
        AttackBuffSfxPath
    ];

    private bool _egoQueued;
    private bool _pheromoneTriggered;
    private bool _chainedLoyaltyPending;
    private bool _buffComboUsed;
    private bool _backgroundMoonTextLoopStarted;
    private MoveState? _warlikeEnhancementState;

    public override int LiberationPhase => Phase;

    protected override bool ReviveTriggerPlaysHitAnimation => true;

    protected override string? ReviveAndEmpowerAnimation => "Cast";

    public bool BuffComboUsed
    {
        get => _buffComboUsed;
        set => _buffComboUsed = value;
    }

    public bool IsWarlikeEnhancementQueuedForCurrentTurn =>
        _pheromoneTriggered
        && _warlikeEnhancementState != null
        && ReferenceEquals(NextMove, _warlikeEnhancementState);

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 201, 198);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 204, 202);

    private static int BrandDamageA =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private static int BrandDamageB =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private static int KingdomDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    private static int PunishmentStrikeDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, HistoryFloorEgoNumbers.PunishmentStrikeAscensionDamage, HistoryFloorEgoNumbers.PunishmentStrikeBaseDamage);

    public override IEnumerable<string> AssetPaths =>
        HistoryFloorWaspBossCreatureVisuals.Profile.AssetPaths
            .Concat(OverlayAssetPaths)
            .Concat(SfxAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _egoQueued = false;
        _pheromoneTriggered = false;
        _backgroundMoonTextLoopStarted = false;
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<PunishmentStrikeEgoCard>());

        if (CombatState?.Encounter is HistoryFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        HistoryFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<HistoryFloorCorrosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<HistoryFloorWaspExpansionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<HistoryFloorWaspPheromonePower>(Creature, 1m, Creature, null, silent: true);
        StartBackgroundMoonTextLoop();
    }

    public override Task BeforeCombatStart()
    {
        HistoryFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        StartBackgroundMoonTextLoop();
        return Task.CompletedTask;
    }

    public override void BeforeRemovedFromRoom()
    {
        StopBackgroundMoonTextLoop();
        base.BeforeRemovedFromRoom();
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature
            || Creature.CombatState?.Encounter is not HistoryFloorLiberationEncounter encounter)
        {
            return Task.CompletedTask;
        }

        return encounter.OnPhaseBossDeath(this, wasRemovalPrevented, deathAnimLength);
    }

    public async Task QueueWarlikeEnhancement()
    {
        if (_pheromoneTriggered || Creature.IsDead || _warlikeEnhancementState == null)
        {
            return;
        }

        _pheromoneTriggered = true;
        _chainedLoyaltyPending = true;
        SetMoveImmediate(_warlikeEnhancementState, forceTransition: true);

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

    public void QueueWarlikeEnhancementForNextRoll()
    {
        if (_pheromoneTriggered || Creature.IsDead || _warlikeEnhancementState == null)
        {
            return;
        }

        _pheromoneTriggered = true;
        _chainedLoyaltyPending = true;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        var gloriousBrand = new MoveState(
            GloriousBrandMoveId,
            GloriousBrandMove,
            CreateSporeAttackIntent(BrandDamageA),
            CreateSporeAttackIntent(BrandDamageB),
            CreateSporeAttackIntent(BrandDamageA));

        var loyaltyEnhancement = new MoveState(
            LoyaltyEnhancementMoveId,
            LoyaltyEnhancementMove,
            new DefendIntent(),
            new DetailedBuffIntent<LibraryEndurancePower>(LoyaltyGuard, DetailedBuffTargetScope.OtherEnemies));

        var warlikeEnhancement = new MoveState(
            WarlikeEnhancementMoveId,
            WarlikeEnhancementMove,
            new DefendIntent(),
            new DetailedBuffIntent<StrengthPower>(WarlikeStrength, DetailedBuffTargetScope.OtherEnemies),
            new DebuffIntent());

        var forTheKingdom = new MoveState(
            ForTheKingdomMoveId,
            ForTheKingdomMove,
            CreateSporeAttackIntent(KingdomDamage),
            CreateSporeAttackIntent(KingdomDamage));

        var punishmentStrike = new MoveState(
            PunishmentStrikeMoveId,
            PunishmentStrikeMove,
            new PlayCardAttackIntent<PunishmentStrikeEgoCard>(
                "PUNISHMENT_STRIKE_EGO_CARD",
                () => PunishmentStrikeDamage,
                static (card, damages) => { card.UpgradePreview(); card.SetPreviewDamage(damages[0]); }),
            new DebuffIntent(true)    
        );

        _warlikeEnhancementState = warlikeEnhancement;

        var randomBranch = new RandomBranchState("RAND");
        randomBranch.AddBranch(gloriousBrand, MoveRepeatType.CannotRepeat);
        randomBranch.AddBranch(loyaltyEnhancement, MoveRepeatType.CannotRepeat);
        randomBranch.AddBranch(forTheKingdom, MoveRepeatType.CannotRepeat);

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(punishmentStrike, () => _egoQueued);
        chooser.AddState(warlikeEnhancement, () => _pheromoneTriggered);
        chooser.AddState(randomBranch, () => true);

        var afterWarlike = new ConditionalBranchState("AFTER_WARLIKE");
        afterWarlike.AddState(loyaltyEnhancement, () => _chainedLoyaltyPending);
        afterWarlike.AddState(chooser, () => true);

        gloriousBrand.FollowUpState = chooser;
        loyaltyEnhancement.FollowUpState = chooser;
        warlikeEnhancement.FollowUpState = afterWarlike;
        forTheKingdom.FollowUpState = chooser;
        punishmentStrike.FollowUpState = chooser;
        reviveAndEmpower.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[] { reviveAndEmpower, gloriousBrand, loyaltyEnhancement, warlikeEnhancement, forTheKingdom, punishmentStrike, randomBranch, chooser, afterWarlike },
            chooser);
    }

    private async Task GloriousBrandMove(IReadOnlyList<Creature> targets)
    {
        await ApplySporeFromResults(await ExecuteAttackSegment("Attack", BrandDamageA));
        await ApplySporeFromResults(await ExecuteAttackSegment("AttackPierce", BrandDamageB));
        await ApplySporeFromResults(await ExecuteAttackSegment("Attack", BrandDamageA));
    }

    private async Task LoyaltyEnhancementMove(IReadOnlyList<Creature> targets)
    {
        if (_chainedLoyaltyPending)
        {
            HistoryFloorLiberationBackgroundController.PlayWaspBuffOverlay();
        }

        LocalOggOneShotPlayer.Play(AttackBuffSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Defend", 0.45f);
        await CreatureCmd.GainBlock(Creature, LoyaltyBlock, ValueProp.Move, null);

        IReadOnlyList<Creature> allies = CombatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Where(enemy => enemy != Creature)
            .ToArray();
        if (allies.Count > 0)
        {
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                new ThrowingPlayerChoiceContext(),
                allies,
                LoyaltyGuard,
                LoyaltyGuardTurns,
                IsPermanent: false,
                Creature,
                null);
        }

        _chainedLoyaltyPending = false;
    }

    private async Task WarlikeEnhancementMove(IReadOnlyList<Creature> targets)
    {
        HistoryFloorLiberationBackgroundController.PlayWaspBuffOverlay();
        LocalOggOneShotPlayer.Play(AttackBuffSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.45f);
        await CreatureCmd.GainBlock(Creature, WarlikeBlock, ValueProp.Move, null);

        IReadOnlyList<Creature> allies = CombatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Where(enemy => enemy != Creature)
            .ToArray();
        foreach (Creature ally in allies)
        {
            await PowerCmdCompat.Apply<StrengthPower>(ally, WarlikeStrength, Creature, null);
        }
        IReadOnlyList<Creature> players = CombatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();
        foreach (var target in players)
        {
            await PowerCmdCompat.Apply<WeakPower>(target, WarlikeWeak, Creature, null);
        }

        _buffComboUsed = true;
        _pheromoneTriggered = false;
        _egoQueued = true;
    }

    private async Task ForTheKingdomMove(IReadOnlyList<Creature> targets)
    {
        await ApplySporeFromResults(await ExecuteAttackSegment("AttackPierce", KingdomDamage));
        await ApplySporeFromResults(await ExecuteAttackSegment("AttackPierce", KingdomDamage));
    }

    private async Task PunishmentStrikeMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackBuffSfxPath, -2f);
        AttackCommand attack = await DamageCmd.Attack(PunishmentStrikeDamage)
            .FromMonster(this)
            .WithAttackerAnim("AttackPierce", 1.05f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        IReadOnlyList<Creature> liveTargets = AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsAlive)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
        if (liveTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(liveTargets, HistoryFloorEgoNumbers.PunishmentStrikeConfusionAmount, Creature, null);
            foreach (Creature target in liveTargets)
            {
                await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                    target,
                    HistoryFloorEgoNumbers.PunishmentStrikeVulnerableAmount,
                    turns: -1,
                    Creature,
                    null);
            }
        }

        _egoQueued = false;
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter is HistoryFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private Task<AttackCommand> ExecuteSegmentAttack(string trigger, int damage)
    {
        LocalOggOneShotPlayer.Play(AttackBuffSfxPath, -2f);
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(trigger, SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteAttackSegment(string trigger, int damage)
    {
        if (Creature == null || Creature.CombatState == null)
        {
            return Array.Empty<DamageResult>();
        }

        AttackCommand attack = await ExecuteSegmentAttack(trigger, damage);
        return AttackCommandCompat.Results(attack);
    }

    private async Task ApplySporeFromResults(IReadOnlyList<DamageResult> results)
    {
        IReadOnlyList<Creature> targets = results
            .Where(static result => result.Receiver.IsPlayer && result.Receiver.IsAlive && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
        if (targets.Count > 0)
        {
            await PowerCmdCompat.Apply<HistoryFloorWaspSporePower>(targets, 1m, Creature, null);
        }
    }

    private void StartBackgroundMoonTextLoop()
    {
        if (_backgroundMoonTextLoopStarted || Creature == null || Creature.IsDead)
        {
            return;
        }

        _backgroundMoonTextLoopStarted = true;
        MoonTextService.StartRandomLoop(
            Creature,
            BackgroundTextScope,
            BackgroundTextLineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    internal void StopBackgroundMoonTextLoop()
    {
        if (Creature != null)
        {
            MoonTextService.StopRandomLoop(Creature, BackgroundTextScope);
        }
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return CreateSporeAttackIntent(BrandDamageA);
        yield return CreateSporeAttackIntent(BrandDamageB);
        yield return new DefendIntent();
        yield return new DetailedBuffIntent<LibraryEndurancePower>(LoyaltyGuard, DetailedBuffTargetScope.OtherEnemies);
        yield return new DetailedBuffIntent<StrengthPower>(WarlikeStrength, DetailedBuffTargetScope.OtherEnemies);
        yield return new DebuffIntent();
        yield return new PlayCardAttackIntent<PunishmentStrikeEgoCard>(
            "PUNISHMENT_STRIKE_EGO_CARD",
            () => PunishmentStrikeDamage,
            static (card, damages) => { card.UpgradePreview(); card.SetPreviewDamage(damages[0]); });
        yield return new DetailedBuffIntent<LibraryOfRuinaConfusionPower>(HistoryFloorEgoNumbers.PunishmentStrikeConfusionAmount, DetailedBuffTargetScope.Target);
        yield return new DetailedBuffIntent<LibraryVulnerablePower>(HistoryFloorEgoNumbers.PunishmentStrikeVulnerableAmount, DetailedBuffTargetScope.Target);
        yield return new HealIntent();
        yield return new BuffIntent();
    }

    private static BadgedAttackIntent CreateSporeAttackIntent(int damage) =>
        new(damage, "WASP_UNBLOCKED_SPORE_ATTACK.description", IntentBadge.FromPower<HistoryFloorWaspSporePower>(1));
}
