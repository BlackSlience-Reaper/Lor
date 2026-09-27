using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.afflictions.TechnologyFloorLiberation;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.TechnologyFloorLiberation;
using LibraryOfRuina.cards.TechnologyFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters.DeadButterfly;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.TechnologyFloorLiberation;
using LibraryOfRuina.visuals.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.monsters.TechnologyFloorLiberation;

public sealed class TechnologyFloorSolemnMourningBoss : LibraryMonsterModel, ILiberationPrimaryPhaseBoss
{
    private const int Phase = 4;

    public override int DefaultChaoResistance => 65;

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    private const string SolemnCeremonyMoveId = "SOLEMN_CEREMONY";
    private const string HandOfDeliveranceMoveId = "HAND_OF_DELIVERANCE";
    private const string SinkingIntoBlissMoveId = "SINKING_INTO_BLISS";
    private const string RestingPlaceMoveId = "RESTING_PLACE";
    private const string SolemnMourningEgoMoveId = "SOLEMN_MOURNING_EGO";
    private const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";

    internal const string BackgroundTextScope = "technology_floor_liberation_phase_4";
    private const float BackgroundTextIntervalSeconds = 5f;

    public const string Root = "res://images/monsters/technology_floor/solemn_mourning/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string AttackTexturePath = Root + "attack.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string ParryTexturePath = Root + "parry.png";
    public const string EgoS1TexturePath = Root + "ego_s1.png";
    public const string EgoS2TexturePath = Root + "ego_s2.png";
    public const string EgoFlashWhiteTexturePath = Root + "ego_flash_white.png";
    internal const string EgoFlashLayerNodeName = "SolemnMourningEgoFlashLayer";

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 190f, 980f, 470f);

    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.0",
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.1",
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.2",
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.3",
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.4",
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.5",
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.6",
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.7",
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.8",
        "TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS.backgroundText.normal.9"
    ];

    private const int DrawPileSealCount = 5;
    private const int DrawPileSealCountAscension = 6;
    private const int RedemptionHandInitialThreshold = 3;
    private const int SerenityThreshold = 2;
    private const int ConfusionStacks = 1;

    private static readonly DeadButterflyInitialMove[][] ButterflyPatternsWeak =
    [
        [DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.SpiritRelease, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.PeacefulRepose],
        [DeadButterflyInitialMove.SpiritRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.AngryRelease],
        [DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.SpiritRelease]
    ];

    private static readonly DeadButterflyInitialMove[][] ButterflyPatternsStrong =
    [
        [DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.SpiritRelease, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.PeacefulRepose],
        [DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.SpiritRelease],
        [DeadButterflyInitialMove.SpiritRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.AngryRelease],
        [DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.SpiritRelease]
    ];

    private int _cadenceIndex;
    private bool _isFirstTurn = true;
    private bool _egoQueued;
    private bool _backgroundMoonTextLoopStarted;
    private CanvasLayer? _activeEgoFlashLayer;
    private Tween? _activeEgoFlashTween;
    private MoveState? _egoState;
    private MoveState? _reviveAndEmpowerState;

    public int LiberationPhase => Phase;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 403, 399);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 405, 401);

    private static int HandOfDeliveranceDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 12);

    private static int SinkingIntoBlissDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 15, 14);

    private static int EgoDamage => 3;

    private static int EgoHitCount =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    private static int ButterflyCount =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 4, 3);

    private static int DrawPileSealAmount =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, DrawPileSealCountAscension, DrawPileSealCount);

    private static readonly string SealBadgeIconPath = "powers/solemn_mourning_seal_on_enemy_power.png";

    private static IntentBadge CreateSealBadge() =>
        IntentBadge.Custom(
            SealBadgeIconPath,
            static () => HoverTipFactory.FromAffliction<SolemnMourningPersistentSealAffliction>(),
            1);

    public override IEnumerable<string> AssetPaths =>
        TechnologyFloorSolemnMourningBossCreatureVisuals.Profile.AssetPaths
            .Append(EgoFlashWhiteTexturePath)
            .Concat(new[]
            {
                FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.AttackWhiteSfxPath,
                FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.AttackBlackSfxPath
            })
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _cadenceIndex = 0;
        _isFirstTurn = true;
        _egoQueued = false;
        _backgroundMoonTextLoopStarted = false;

        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<SolemnMourningEgoCard>());

        if (CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        TechnologyFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<TechnologyFloorErosionPower>(
            Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<SolemnMourningSealPower>(
            Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<SolemnMourningRedemptionHandPower>(
            Creature, RedemptionHandInitialThreshold, Creature, null, silent: true);
        await PowerCmdCompat.Apply<SolemnMourningSerenityPower>(
            Creature, SerenityThreshold, Creature, null, silent: true);

        StartBackgroundMoonTextLoop();
    }

    public override Task BeforeCombatStart()
    {
        TechnologyFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        StartBackgroundMoonTextLoop();
        return Task.CompletedTask;
    }

    private void StartBackgroundMoonTextLoop()
    {
        if (!_backgroundMoonTextLoopStarted && Creature.IsAlive)
        {
            _backgroundMoonTextLoopStarted = true;
            MoonTextService.StartRandomLoop(
                Creature,
                BackgroundTextScope,
                NormalBackgroundTextLineKeys.Select(L10NMonsterLookup).ToArray(),
                BackgroundTextIntervalSeconds,
                BackgroundTextSpawnArea);
        }
    }

    internal void StopBackgroundMoonTextLoop()
    {
        if (Creature != null)
        {
            MoonTextService.StopRandomLoop(Creature, BackgroundTextScope);
        }
    }

    public override void BeforeRemovedFromRoom()
    {
        CleanupActiveEgoFlashLayer();
        StopBackgroundMoonTextLoop();
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature
            || Creature.CombatState?.Encounter is not TechnologyFloorLiberationEncounter encounter)
        {
            return Task.CompletedTask;
        }

        CleanupActiveEgoFlashLayer();
        return encounter.OnPhaseBossDeath(this, wasRemovalPrevented, deathAnimLength);
    }

    public async Task TriggerReviveAndEmpowerState()
    {
        if (NCombatRoom.Instance?.GetCreatureNode(Creature) != null)
        {
            await CreatureCmd.TriggerAnim(Creature, "Hit", 0f);
        }

        ForceReviveAndEmpowerState();
    }

    public void ForceReviveAndEmpowerState()
    {
        if (_reviveAndEmpowerState != null)
        {
            SetMoveImmediate(_reviveAndEmpowerState, forceTransition: true);
        }
    }

    internal async Task<bool> QueueEgoSequence()
    {
        if (Creature.IsDead || _egoState == null)
        {
            return false;
        }

        _egoQueued = true;
        if (IsPerformingMove)
        {
            return true;
        }

        SetMoveImmediate(_egoState, forceTransition: true);

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }

        return true;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _reviveAndEmpowerState = new LibraryPhaseTransitionMoveState(
            ReviveAndEmpowerMoveId,
            ReviveAndEmpowerMove,
            new HealIntent(),
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        var solemnCeremony = new MoveState(
            SolemnCeremonyMoveId,
            SolemnCeremonyMove,
            new CardDebuffIntent(),
            new SummonIntent());

        var handOfDeliverance = new MoveState(
            HandOfDeliveranceMoveId,
            HandOfDeliveranceMove,
            new BadgedAttackIntent(
                HandOfDeliveranceDamage,
                CreateSealBadge(),
                "SOLEMN_MOURNING_HAND_OF_DELIVERANCE.description"),
            new BadgedAttackIntent(
                HandOfDeliveranceDamage,
                CreateSealBadge(),
                "SOLEMN_MOURNING_HAND_OF_DELIVERANCE.description"));

        var sinkingIntoBliss = new MoveState(
            SinkingIntoBlissMoveId,
            SinkingIntoBlissMove,
            new BadgedAttackIntent(
                SinkingIntoBlissDamage,
                CreateSealBadge(),
                "SOLEMN_MOURNING_SINKING_INTO_BLISS.description"),
            new BadgedAttackIntent(
                SinkingIntoBlissDamage,
                CreateSealBadge(),
                "SOLEMN_MOURNING_SINKING_INTO_BLISS.description"));

        var restingPlace = new MoveState(
            RestingPlaceMoveId,
            RestingPlaceMove,
            new BuffIntent(),
            new DebuffIntent());

        var solemnMourningEgo = new MoveState(
            SolemnMourningEgoMoveId,
            SolemnMourningEgoMove,
            new PlayCardAttackIntent<SolemnMourningEgoCard>(
                "SOLEMN_MOURNING_EGO_CARD",
                () => EgoDamage,
                (card, damages) =>
                {
                    card.UpgradePreview();
                    card.SetPreviewDamage(damages[0], EgoHitCount);
                },
                () => EgoHitCount));
        _egoState = solemnMourningEgo;

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(solemnMourningEgo, () => _egoQueued);
        chooser.AddState(solemnCeremony, () => _isFirstTurn);
        chooser.AddState(handOfDeliverance, () => _cadenceIndex == 0);
        chooser.AddState(sinkingIntoBliss, () => _cadenceIndex == 1);
        chooser.AddState(restingPlace, () => true);

        solemnCeremony.FollowUpState = chooser;
        handOfDeliverance.FollowUpState = chooser;
        sinkingIntoBliss.FollowUpState = chooser;
        restingPlace.FollowUpState = chooser;
        solemnMourningEgo.FollowUpState = chooser;
        _reviveAndEmpowerState.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[]
            {
                _reviveAndEmpowerState, solemnCeremony, handOfDeliverance,
                sinkingIntoBliss, restingPlace, solemnMourningEgo, chooser
            },
            chooser);
    }

    private async Task SolemnCeremonyMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);

        int sealCount = DrawPileSealAmount;
        if (Creature.CombatState is { } combatState)
        {
            foreach (var player in combatState.Players)
            {
                if (player.PlayerCombatState == null)
                {
                    continue;
                }

                var allCards = player.PlayerCombatState.DrawPile.Cards
                    .Concat(player.PlayerCombatState.DiscardPile.Cards)
                    .Where(static c => !SolemnMourningPersistentSealAffliction.IsAnySeal(c))
                    .ToArray();

                GD.Print($"[SolemnCeremony] Eligible cards for sealing: {allCards.Length}, sealCount: {sealCount}");

                foreach (CardModel card in allCards.TakeRandom(
                    sealCount, combatState.RunState.Rng.CombatCardSelection))
                {
                    var result = await CardCmd.Afflict<SolemnMourningPersistentSealAffliction>(card, 1);
                    GD.Print($"[SolemnCeremony] Afflict {card.Id} in {card.Pile?.Type}: result={result != null}");
                }
            }
        }

        await SummonButterflies();
        _isFirstTurn = false;
    }

    private async Task HandOfDeliveranceMove(IReadOnlyList<Creature> targets)
    {
        int pendingSelfSeals = 0;

        if (!await TryConsumeSealStack())
        {
            AttackCommand? attack1Result = await ExecuteAttackSegment("Attack", HandOfDeliveranceDamage);
            DamageResult? result1 = AttackCommandCompat.Results(attack1Result).FirstOrDefault();
            if (result1 != null)
            {
                if (result1.UnblockedDamage > 0)
                {
                    await SealRandomHandCard(result1.Receiver);
                }
                else if (result1.BlockedDamage > 0)
                {
                    pendingSelfSeals++;
                }
            }
        }

        if (!await TryConsumeSealStack())
        {
            AttackCommand? attack2Result = await ExecuteAttackSegment("Attack", HandOfDeliveranceDamage);
            DamageResult? result2 = AttackCommandCompat.Results(attack2Result).FirstOrDefault();
            if (result2 != null)
            {
                if (result2.UnblockedDamage > 0)
                {
                    await SealRandomHandCard(result2.Receiver);
                }
                else if (result2.BlockedDamage > 0)
                {
                    pendingSelfSeals++;
                }
            }
        }

        if (pendingSelfSeals > 0)
        {
            await PowerCmdCompat.Apply<SolemnMourningSealOnEnemyPower>(
                Creature, pendingSelfSeals, Creature, null);
        }

        AdvanceCadence();
    }

    private async Task SinkingIntoBlissMove(IReadOnlyList<Creature> targets)
    {
        if (!await TryConsumeSealStack())
        {
            AttackCommand? attack1Result = await ExecuteAttackSegment("Attack", SinkingIntoBlissDamage);
            DamageResult? result1 = AttackCommandCompat.Results(attack1Result).FirstOrDefault();
            if (result1 is { UnblockedDamage: > 0 })
            {
                await SealRandomHandCard(result1.Receiver);
            }
        }

        if (!await TryConsumeSealStack())
        {
            AttackCommand? attack2Result = await ExecuteAttackSegment("Attack", SinkingIntoBlissDamage);
            DamageResult? result2 = AttackCommandCompat.Results(attack2Result).FirstOrDefault();
            if (result2 is { UnblockedDamage: > 0 })
            {
                await SealRandomHandCard(result2.Receiver);
            }
        }

        AdvanceCadence();
    }

    private async Task RestingPlaceMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);

        var redemptionHand = Creature.GetPower<SolemnMourningRedemptionHandPower>();
        redemptionHand?.EscalateFromRestingPlace();

        if (Creature.CombatState is { } combatState)
        {
            IReadOnlyList<Creature> players = combatState.PlayerCreatures
                .Where(static p => p.IsAlive)
                .ToArray();

            if (players.Count > 0)
            {
                await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
                    players, ConfusionStacks, Creature, null);
            }
        }

        AdvanceCadence();
    }

    private async Task SolemnMourningEgoMove(IReadOnlyList<Creature> targets)
    {
        int hits = EgoHitCount;

        CanvasLayer? flashLayer = null;
        TextureRect? flashRect = null;
        Tween? flashTween = null;
        bool flashFadeStarted = false;

        try
        {
            NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
            if (creatureNode != null)
            {
                Texture2D? flashTexture = GD.Load<Texture2D>(EgoFlashWhiteTexturePath);
                if (flashTexture != null)
                {
                    flashLayer = new CanvasLayer
                    {
                        Name = EgoFlashLayerNodeName,
                        Layer = 100
                    };
                    flashRect = new TextureRect
                    {
                        Texture = flashTexture,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.Scale,
                        AnchorRight = 1f,
                        AnchorBottom = 1f,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                        Modulate = new Color(1f, 1f, 1f, 0f)
                    };
                    flashLayer.AddChild(flashRect);
                    creatureNode.GetTree().Root.AddChild(flashLayer);
                    _activeEgoFlashLayer = flashLayer;

                    flashTween = creatureNode.CreateTween();
                    _activeEgoFlashTween = flashTween;
                    flashTween.TweenProperty(flashRect, "modulate:a", 0.7f, 0.3f);
                }
            }

            await CreatureCmd.TriggerAnim(Creature, "EgoS1", 1.0f);

            for (int i = 0; i < hits; i++)
            {
                if (Creature.IsDead)
                {
                    break;
                }

                LocalOggOneShotPlayer.Play(FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.AttackWhiteSfxPath, -2f);

                await DamageCmd.Attack(EgoDamage)
                    .FromMonster(this)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(null);

                if (Creature.IsDead)
                {
                    break;
                }

                if (i < hits - 1)
                {
                    await Cmd.CustomScaledWait(0.04f, 0.08f);
                }
            }

            if (!Creature.IsDead && flashRect != null && GodotObject.IsInstanceValid(flashRect))
            {
                flashTween?.Kill();
                flashFadeStarted = true;
                var fadeOutTween = flashRect.CreateTween();
                fadeOutTween.TweenProperty(flashRect, "modulate:a", 0f, 0.5f);
                fadeOutTween.TweenCallback(Callable.From(() =>
                {
                    QueueFreeActiveEgoFlashLayer(flashLayer);
                }));
            }
        }
        finally
        {
            _egoQueued = false;
            _cadenceIndex = 0;
            if (!flashFadeStarted)
            {
                CleanupEgoFlashLayer(flashLayer, flashTween);
            }
        }
    }

    private void CleanupActiveEgoFlashLayer()
    {
        CleanupEgoFlashLayer(_activeEgoFlashLayer, _activeEgoFlashTween);
        _activeEgoFlashLayer = null;
        _activeEgoFlashTween = null;
    }

    private void QueueFreeActiveEgoFlashLayer(CanvasLayer? flashLayer)
    {
        QueueFreeEgoFlashLayer(flashLayer);
        if (ReferenceEquals(_activeEgoFlashLayer, flashLayer))
        {
            _activeEgoFlashLayer = null;
            _activeEgoFlashTween = null;
        }
    }

    private static void CleanupEgoFlashLayer(CanvasLayer? flashLayer, Tween? flashTween)
    {
        if (flashTween != null && flashTween.IsValid())
        {
            flashTween.Kill();
        }

        QueueFreeEgoFlashLayer(flashLayer);
    }

    private static void QueueFreeEgoFlashLayer(CanvasLayer? flashLayer)
    {
        if (flashLayer != null && GodotObject.IsInstanceValid(flashLayer))
        {
            flashLayer.QueueFree();
        }
    }

    private async Task ReviveAndEmpowerMove(IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);
        await Cmd.CustomScaledWait(0.3f, 0.6f);

        if (Creature.CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseTransition(this);
        }
    }

    private Task<AttackCommand> ExecuteAttackSegment(string animation, int damage)
    {
        LocalOggOneShotPlayer.Play(FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.AttackWhiteSfxPath, -2f);
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(animation, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task<bool> TryConsumeSealStack()
    {
        var sealPower = Creature.GetPower<SolemnMourningSealOnEnemyPower>();
        if (sealPower == null || sealPower.Amount <= 0)
        {
            return false;
        }

        await PowerCmd.Decrement(sealPower);
        return true;
    }

    private async Task SealRandomHandCard(Creature target)
    {
        if (target.Player?.PlayerCombatState == null)
        {
            return;
        }

        var eligibleCards = target.Player.PlayerCombatState.Hand.Cards
            .Concat(target.Player.PlayerCombatState.DrawPile.Cards)
            .Concat(target.Player.PlayerCombatState.DiscardPile.Cards)
            .Where(static c => !SolemnMourningPersistentSealAffliction.IsAnySeal(c))
            .ToArray();

        GD.Print($"[SealRandomHand] hand={target.Player.PlayerCombatState.Hand.Cards.Count}, eligible={eligibleCards.Length}");

        if (eligibleCards.Length == 0)
        {
            return;
        }

        CardModel? card = null;
        if (Creature.CombatState is { } combatState)
        {
            card = eligibleCards
                .TakeRandom(1, combatState.RunState.Rng.CombatCardSelection)
                .FirstOrDefault();
        }

        if (card != null)
        {
            var result = await CardCmd.Afflict<SolemnMourningPersistentSealAffliction>(card, 1);
            GD.Print($"[SealRandomHand] Afflicted {card.Id} in {card.Pile?.Type}: result={result != null}");
        }
    }

    private static readonly string[] ButterflySlots =
    [
        TechnologyFloorLiberationEncounter.SolemnButterflySlotOne,
        TechnologyFloorLiberationEncounter.SolemnButterflySlotTwo,
        TechnologyFloorLiberationEncounter.SolemnButterflySlotThree,
        TechnologyFloorLiberationEncounter.SolemnButterflySlotFour
    ];

    private async Task SummonButterflies()
    {
        if (Creature.CombatState == null)
        {
            return;
        }

        int count = ButterflyCount;
        DeadButterflyInitialMove[][] patterns = count >= 4
            ? ButterflyPatternsStrong
            : ButterflyPatternsWeak;

        for (int i = 0; i < Math.Min(count, Math.Min(patterns.Length, ButterflySlots.Length)); i++)
        {
            string slot = ButterflySlots[i];
            if (Creature.CombatState.Enemies.Any(e => e.SlotName == slot && e.IsAlive))
            {
                continue;
            }

            var butterfly = (DeadButterfly.DeadButterfly)ModelDb.Monster<DeadButterfly.DeadButterfly>().ToMutable();
            butterfly.ConfigureMoveSequence(patterns[i]);
            Creature summoned = await CreatureCmd.Add(
                butterfly, Creature.CombatState, CombatSide.Enemy, slot);
            await PowerCmdCompat.Apply<MinionPower>(summoned, 1m, Creature, null, silent: true);
        }

        Creature.CombatState.SortEnemiesBySlotName();
    }

    private void AdvanceCadence()
    {
        _cadenceIndex = (_cadenceIndex + 1) % 3;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new BadgedAttackIntent(
            HandOfDeliveranceDamage,
            CreateSealBadge(),
            "SOLEMN_MOURNING_HAND_OF_DELIVERANCE.description");
        yield return new BadgedAttackIntent(
            SinkingIntoBlissDamage,
            CreateSealBadge(),
            "SOLEMN_MOURNING_SINKING_INTO_BLISS.description");
        yield return new DebuffIntent();
        yield return new BuffIntent();
        yield return new SummonIntent();
        yield return new HealIntent();
        yield return new PlayCardAttackIntent<SolemnMourningEgoCard>(
            "SOLEMN_MOURNING_EGO_CARD",
            () => EgoDamage,
            (card, damages) =>
            {
                if (AscensionHelper.HasAscension(AscensionLevel.DeadlyEnemies))
                {
                    card.UpgradePreview();
                }

                card.SetPreviewDamage(damages[0], EgoHitCount);
            },
            () => EgoHitCount);
    }
}
