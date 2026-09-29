using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.backgrounds.LanguageFloorLiberation;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.events.LanguageFloorLiberation;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.monsters.LanguageFloorLiberation;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.scene_transitions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.LanguageFloorLiberation;

public sealed class LanguageFloorLiberationEncounter :
    LiberationEncounterBase,
    IEncounterBgmSource,
    ILiberationPhaseBgmSource,
    IFloorLiberationEncounter
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.PhaseBased(
        "LanguageFloorLiberationBGM",
        LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
        volumeScale: 0.85f);

    public const int MaxPhase = 5;
    private const decimal PhaseTransitionHealAmount = 6m;

    internal const float EncounterCameraScaling = 0.82f;
    internal static readonly Vector2 EncounterCameraOffset =
        Vector2.Down * 50f + Vector2.Left * 100f;

    private const string CurrentPhaseKey = "CurrentPhase";
    private const string PhaseCompleteKey = "PhaseComplete";
    private const string TransitionPendingKey = "TransitionPending";
    private const string KilledBossCountKey = "KilledBossCount";
    private const string SettlementTriggeredKey = "SettlementTriggered";
    private const string EndedByLethalDamageKey = "EndedByLethalDamage";

    public const string ScarletSlot = "scarlet_scar";
    public const string WolfSlot = "lost_everything_wolf";
    public const string CorpseSlotOne = "melting_corpse_1";
    public const string CorpseSlotTwo = "melting_corpse_2";
    public const string CorpseSlotThree = "melting_corpse_3";
    public const string CorpseSlotFour = "melting_corpse_4";
    public const string DipsiaLeftBatSlot = "dipsia_bat_left";
    public const string DipsiaBossSlot = "dipsia";
    public const string DipsiaRightBatSlot = "dipsia_bat_right";
    public const string MimicryBossSlot = "mimicry";
    public static readonly IReadOnlyList<string> CorpseSlots =
    [
        CorpseSlotOne,
        CorpseSlotTwo,
        CorpseSlotThree,
        CorpseSlotFour
    ];
    public const string EncounterScenePath =
        "res://scenes/encounters/language_floor_liberation_encounter.tscn";

    public const string BossNodeResourcePath =
        "res://images/map/placeholder/language_floor_liberation_encounter_icon";

    private bool _shouldReducePlayers;
    public static readonly string[] RolandLiberationBgmTracks =
    [
        "res://audio/bgm/language_floor_liberation/roland_liberation_phase_1.ogg",
        "res://audio/bgm/language_floor_liberation/roland_liberation_phase_2.ogg",
        "res://audio/bgm/language_floor_liberation/roland_liberation_phase_3.ogg"
    ];

    public override RoomType RoomType => RoomType.Boss;

    public override bool ShouldGiveRewards => false;

    public override bool HasScene => true;

    public override float GetCameraScaling() => EncounterCameraScaling;

    public override Vector2 GetCameraOffset() => EncounterCameraOffset;

    public override IReadOnlyList<string> Slots =>
        [
            ScarletSlot,
            WolfSlot,
            ..CorpseSlots,
            DipsiaLeftBatSlot,
            DipsiaBossSlot,
            DipsiaRightBatSlot,
            MimicryBossSlot
        ];

    protected override bool HasCustomBackground => true;

    public override string BossNodePath => BossNodeResourcePath;

    public int CurrentPhase { get; private set; } = 1;

    public bool PhaseComplete { get; private set; }

    public bool TransitionPending { get; private set; }

    public int KilledBossCount { get; private set; }

    public bool SettlementTriggered { get; private set; }

    public bool EndedByLethalDamage { get; private set; }

    public string LiberationFloorId =>
        LiberationFloorIds.Language;

    public bool IsFullyLiberated => KilledBossCount >= MaxPhase;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<LanguageFloorScarletScar>(),
        ModelDb.Monster<LanguageFloorLostEverythingWolf>(),
        ModelDb.Monster<LanguageFloorCobaltScar>(),
        ModelDb.Monster<LanguageFloorSmilingFace>(),
        ModelDb.Monster<LanguageFloorMeltingCorpse>(),
        ModelDb.Monster<LanguageFloorDipsia>(),
        ModelDb.Monster<LanguageFloorBloodBat>(),
        ModelDb.Monster<LanguageFloorMimicry>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<LanguageFloorScarletScar>().AssetPaths
            .Concat(ModelDb.Monster<LanguageFloorLostEverythingWolf>().AssetPaths)
            .Concat(ModelDb.Monster<LanguageFloorCobaltScar>().AssetPaths)
            .Concat(ModelDb.Monster<LanguageFloorSmilingFace>().AssetPaths)
            .Concat(ModelDb.Monster<LanguageFloorMeltingCorpse>().AssetPaths)
            .Concat(ModelDb.Monster<LanguageFloorDipsia>().AssetPaths)
            .Concat(ModelDb.Monster<LanguageFloorBloodBat>().AssetPaths)
            .Concat(ModelDb.Monster<LanguageFloorMimicry>().AssetPaths)
            .Concat(new[]
            {
                "res://images/backgrounds/language_floor_liberation_encounter/background_1.png",
                "res://images/backgrounds/language_floor_liberation_encounter/background_2.png",
                LanguageFloorLiberationBackgroundController.PhaseThreeTexturePath,
                LanguageFloorLiberationBackgroundController.PhaseFourTexturePath,
                LanguageFloorLiberationBackgroundController.PhaseFiveTexturePath,
                EncounterScenePath,
                "res://scenes/backgrounds/language_floor_liberation_encounter/language_floor_liberation_encounter_background.tscn",
                LanguageFloorLiberationBackgroundController.NormalTexturePath,
                LanguageFloorLiberationBackgroundController.RageTexturePath,
                "res://images/powers/language_floor_hunt_mark_power.png",
                "res://images/powers/language_floor_scar_power.png",
                "res://images/powers/language_floor_rage_power.png",
                "res://images/powers/language_floor_unrelieved_anger_power.png",
                "res://images/ui/run_history/language_floor_liberation_encounter.png",
                "res://images/ui/run_history/language_floor_liberation_encounter_outline.png",
                BossNodeResourcePath + ".png",
                BossNodeResourcePath + "_outline.png"
            })
            .Concat(RolandLiberationBgmTracks)
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        if (CurrentPhase >= 5)
        {
            return
            [
                (
                    ModelDb.Monster<LanguageFloorMimicry>().ToMutable(),
                    MimicryBossSlot
                )
            ];
        }

        if (CurrentPhase >= 4)
        {
            return
            [
                (
                    ModelDb.Monster<LanguageFloorBloodBat>().ToMutable(),
                    DipsiaLeftBatSlot
                ),
                (
                    ModelDb.Monster<LanguageFloorDipsia>().ToMutable(),
                    DipsiaBossSlot
                ),
                (
                    ModelDb.Monster<LanguageFloorBloodBat>().ToMutable(),
                    DipsiaRightBatSlot
                )
            ];
        }

        if (CurrentPhase == 3)
        {
            return
            [
                (ModelDb.Monster<LanguageFloorSmilingFace>().ToMutable(), WolfSlot)
            ];
        }

        if (CurrentPhase == 2)
        {
            return
            [
                (ModelDb.Monster<LanguageFloorCobaltScar>().ToMutable(), WolfSlot)
            ];
        }

        return
        [
            (ModelDb.Monster<LanguageFloorScarletScar>().ToMutable(), ScarletSlot),
            (ModelDb.Monster<LanguageFloorLostEverythingWolf>().ToMutable(), WolfSlot)
        ];
    }

    public override Dictionary<string, string> SaveCustomState() =>
        new()
        {
            [CurrentPhaseKey] = CurrentPhase.ToString(),
            [PhaseCompleteKey] = PhaseComplete.ToString(),
            [TransitionPendingKey] = TransitionPending.ToString(),
            [KilledBossCountKey] = KilledBossCount.ToString(),
            [SettlementTriggeredKey] = SettlementTriggered.ToString(),
            [EndedByLethalDamageKey] = EndedByLethalDamage.ToString()
        };

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        var bag = new EncounterStateBag(state);
        // 阶段只保证不小于 1，不按 MaxPhase 钳制上限。
        CurrentPhase = Math.Max(1, bag.ReadInt(CurrentPhaseKey, 1));
        PhaseComplete = bag.ReadBool(PhaseCompleteKey);
        TransitionPending = bag.ReadBool(TransitionPendingKey);
        // 没有击杀数的旧档按阶段推算：本阶段已完成则算上本阶段。
        KilledBossCount = bag.ReadClampedInt(
            KilledBossCountKey,
            PhaseComplete ? CurrentPhase : CurrentPhase - 1,
            0,
            MaxPhase);
        SettlementTriggered = bag.ReadBool(SettlementTriggeredKey);
        EndedByLethalDamage = bag.ReadBool(EndedByLethalDamageKey);
    }

    public async Task EnsureControllerPowers(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (Creature player in combatState.PlayerCreatures)
        {
            await PowerCmdCompat.Ensure<
                LanguageFloorLiberationControllerPower>(player);
        }
    }

    public async Task OnBeforeSideTurnStart(
        CombatSide side,
        CombatStateLike combatState)
    {
        await EnsureControllerPowers(combatState);
        if (!PhaseComplete && !SettlementTriggered)
        {
            IReadOnlyList<Creature> restored = await LiberationPhaseCleanup
                .RestoreMissingLiveCreatureNodes(combatState);
            if (restored.Count > 0)
            {
                Log.Warn(
                    "[LanguageFloorLiberation] Restored missing live creature node(s): "
                    + string.Join(
                        ", ",
                        restored.Select(static creature =>
                            creature.Monster?.GetType().Name ?? "<unknown>")));
            }
        }

        if (side != CombatSide.Enemy || !TransitionPending || PhaseComplete)
        {
            return;
        }

        if (combatState.Enemies.Any(static enemy =>
                enemy.Monster is ILiberationPhaseBoss))
        {
            return;
        }

        if (CurrentPhase == 2)
        {
            await SpawnPhaseTwo(combatState);
        }
        else if (CurrentPhase == 3)
        {
            await SpawnPhaseThree(combatState);
        }
        else if (CurrentPhase == 4)
        {
            await SpawnPhaseFour(combatState);
        }
        else if (CurrentPhase == 5)
        {
            await SpawnPhaseFive(combatState);
        }
    }

    public bool ShouldKeepCombatOpen(CombatStateLike combatState)
    {
        if (SettlementTriggered || PhaseComplete)
        {
            return false;
        }

        if (LiberationCombatEndGuard.ShouldKeepCombatOpen(
                combatState,
                CurrentPhase,
                TransitionPending,
                encounterComplete: false))
        {
            return true;
        }

        RecoverMissingTerminalPhaseBoss(combatState);
        return false;
    }

    private void RecoverMissingTerminalPhaseBoss(
        CombatStateLike combatState)
    {
        if (CurrentPhase is not (3 or 5))
        {
            return;
        }

        Creature? currentBoss = CurrentPhase == 3
            ? combatState.Enemies.FirstOrDefault(static enemy =>
                enemy.Monster is LanguageFloorSmilingFace)
            : combatState.Enemies.FirstOrDefault(static enemy =>
                enemy.Monster is LanguageFloorMimicry);
        if (currentBoss?.IsAlive == true)
        {
            return;
        }

        if (currentBoss?.Monster is LanguageFloorMimicry
            {
                Form: LanguageFloorMimicryForm.Third,
                IsFakeDead: false
            })
        {
            KilledBossCount = Math.Max(KilledBossCount, MaxPhase);
        }

        PhaseComplete = true;
        TransitionPending = false;
        SettlementTriggered = KilledBossCount >= 2;
        EndedByLethalDamage = false;
        LanguageFloorLiberationSettlementStore.Record(this);
        LanguageFloorDeathContext.Clear();
        Log.Warn(
            "[LanguageFloorLiberation] Recovered missing terminal phase boss before combat end: phase="
            + CurrentPhase
            + ", settledKills="
            + KilledBossCount
            + ".");
        ScheduleDeferredWinConditionCheck();
    }

    public bool ShouldKeepPhaseBossAfterDeath(Creature creature)
    {
        if (PhaseComplete || SettlementTriggered)
        {
            return false;
        }

        if (CurrentPhase == 3
            && creature.Monster is LanguageFloorSmilingFace
                { ForceKillable: false })
        {
            return true;
        }

        if (CurrentPhase == 5
            && creature.Monster is LanguageFloorMimicry mimicry
            && mimicry.CanEnterFakeDeath(creature))
        {
            return true;
        }

        if (creature.Monster is not ILiberationPrimaryPhaseBoss boss
            || boss.LiberationPhase >= MaxPhase)
        {
            return false;
        }

        if (TransitionPending
            && boss.LiberationPhase + 1 == CurrentPhase)
        {
            return true;
        }

        if (boss.LiberationPhase != CurrentPhase)
        {
            return false;
        }

        if (creature.Monster is not LanguageFloorLostEverythingWolf)
        {
            return true;
        }

        Creature? scarlet = creature.CombatState?.Creatures.FirstOrDefault(
            static candidate =>
                candidate.Monster is LanguageFloorScarletScar);
        return scarlet?.IsDead == true
            || LanguageFloorDeathContext.GetDealer(creature)?.Monster
                is LanguageFloorScarletScar;
    }

    public bool ShouldKeepPhaseThreeBossAfterDeath(Creature creature) =>
        ShouldKeepPhaseBossAfterDeath(creature);

    public bool ShouldSuppressPhaseBossInteraction(Creature creature) =>
        !SettlementTriggered
        && !PhaseComplete
        && ((CurrentPhase == 3
                && creature.Monster is LanguageFloorSmilingFace
                    { IsFakeDead: true })
            || (CurrentPhase == 5
                && creature.Monster is LanguageFloorMimicry
                    { IsFakeDead: true })
            || (TransitionPending
                && creature.Monster is ILiberationPhaseBoss boss
                && boss.LiberationPhase + 1 == CurrentPhase));

    public bool ShouldSuppressPhaseThreeBossInteraction(Creature creature) =>
        ShouldSuppressPhaseBossInteraction(creature);

    public bool ShouldPreventTransitionBossDeath(Creature creature) =>
        !PhaseComplete
        && !SettlementTriggered
        && TransitionPending
        && creature.Monster is ILiberationPhaseBoss boss
        && boss.LiberationPhase + 1 == CurrentPhase;

    public bool ShouldPreventPlayerDeath(Creature creature) =>
        creature.IsPlayer
        && IsLastAlivePlayer(creature)
        && (SettlementTriggered || KilledBossCount >= 2);

    public async Task OnPreventingDeath(Creature creature)
    {
        if (ShouldPreventPlayerDeath(creature))
        {
            await CreatureCmd.SetCurrentHp(creature, 1m);
            if (SettlementTriggered)
            {
                // 结算已在进行：只恢复血量并返回，不重复触发胜利流程。
                return;
            }

            PhaseComplete = true;
            TransitionPending = false;
            SettlementTriggered = true;
            EndedByLethalDamage = true;
            LanguageFloorLiberationSettlementStore.Record(this);

            await EndCombatAsLiberationVictory(
                creature.CombatState,
                deferRecheckIfNotEnded: true);
            return;
        }

        if (ShouldPreventTransitionBossDeath(creature))
        {
            await CreatureCmd.SetCurrentHp(creature, 1m);
        }
    }

    internal async Task ResolvePhaseThreeCreatureDeath(Creature creature)
    {
        if (PhaseComplete
            || CurrentPhase != 3
            || creature.CombatState is not { } combatState)
        {
            return;
        }

        if (creature.Monster is LanguageFloorSmilingFace boss)
        {
            await boss.EnterFakeDeathFromDeath();
            return;
        }

        if (creature.Monster is not LanguageFloorMeltingCorpse)
        {
            return;
        }

        LanguageFloorSmilingFace? activeBoss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorSmilingFace>()
            .FirstOrDefault();
        if (activeBoss != null)
        {
            await activeBoss.OnCorpseDeath();
        }
    }

    internal async Task ResolvePhaseOneDeath(Creature deadCreature, Creature? dealer)
    {
        if (PhaseComplete
            || TransitionPending
            || deadCreature.CombatState is not { } combatState)
        {
            return;
        }

        Creature? scarlet = combatState.Creatures.FirstOrDefault(
            static creature => creature.Monster is LanguageFloorScarletScar);
        Creature? wolf = combatState.Creatures.FirstOrDefault(
            static creature => creature.Monster is LanguageFloorLostEverythingWolf);

        if (deadCreature.Monster is LanguageFloorLostEverythingWolf)
        {
            if (scarlet?.IsDead == true)
            {
                await CompletePhaseOne(
                    combatState,
                    (ILiberationPhaseBoss)deadCreature.Monster,
                    reducePlayers: false);
                return;
            }

            if (dealer?.Monster is LanguageFloorScarletScar)
            {
                await CompletePhaseOne(
                    combatState,
                    (ILiberationPhaseBoss)deadCreature.Monster,
                    reducePlayers: false);
                return;
            }

            if (scarlet?.Monster is LanguageFloorScarletScar scarletModel
                && scarlet.IsAlive)
            {
                await scarletModel.EnterUnrelievedAnger();
            }

            return;
        }

        if (deadCreature.Monster is LanguageFloorScarletScar)
        {
            if (wolf?.IsDead == true)
            {
                await CompletePhaseOne(
                    combatState,
                    (ILiberationPhaseBoss)deadCreature.Monster,
                    reducePlayers: false);
                return;
            }

            await CompletePhaseOne(
                combatState,
                (ILiberationPhaseBoss)deadCreature.Monster,
                reducePlayers: true);
        }
    }

    private async Task CompletePhaseOne(
        CombatStateLike combatState,
        ILiberationPhaseBoss transitionBoss,
        bool reducePlayers)
    {
        if (PhaseComplete || TransitionPending)
        {
            return;
        }

        CurrentPhase = 2;
        TransitionPending = true;
        KilledBossCount = Math.Max(KilledBossCount, 1);
        SettlementTriggered = false;
        EndedByLethalDamage = false;
        LanguageFloorLiberationSettlementStore.Record(this);
        LanguageFloorDeathContext.Clear();
        _shouldReducePlayers = reducePlayers;
        if (reducePlayers)
        {
            foreach (Creature player in combatState.PlayerCreatures.Where(static creature => creature.IsAlive))
            {
                int reducedHp = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        player.CurrentHp
                        * LanguageFloorDeathTrackerPower.PlayerCurrentHpRemainingPercent
                        / 100m));
                await CreatureCmd.SetCurrentHp(player, reducedHp);
            }
        }

        await LiberationPhaseCleanup.RemovePhaseCreatures(
            combatState,
            except: transitionBoss.Creature,
            includeDeadStateCreatures: true);
        await LiberationPhasePlayerRecovery.RestorePlayers(
            combatState,
            PhaseTransitionHealAmount);

        await LiberationPhaseTransition.ShowAsync(
            transitionBoss,
            triggerAnimation: true);

        RefreshLiberationPhaseBgm();
    }

    internal async Task CompletePhaseTransition(
        ILiberationPhaseBoss boss)
    {
        if (!TransitionPending
            || PhaseComplete
            || boss.LiberationPhase + 1 != CurrentPhase
            || boss.Creature.CombatState is not { } combatState)
        {
            return;
        }

        if (CurrentPhase == 2)
        {
            await SpawnPhaseTwo(combatState);
        }
        else if (CurrentPhase == 3)
        {
            await SpawnPhaseThree(combatState);
        }
        else if (CurrentPhase == 4)
        {
            await SpawnPhaseFour(combatState);
        }
        else if (CurrentPhase == 5)
        {
            await SpawnPhaseFive(combatState);
        }
    }

    private async Task SpawnPhaseTwo(CombatStateLike combatState)
    {
        TextureRect? backgroundImage =
            LanguageFloorLiberationBackgroundController
                .GetCurrentBackgroundImage();
        await LiberationPhaseCleanup.RemovePhaseCreatures(combatState);
        if (combatState.Enemies.Any(static enemy => enemy.IsAlive))
        {
            return;
        }

        await LorexSceneTransitionController.PlayPhaseBackgroundRevealAsync(
            backgroundImage,
            LanguageFloorLiberationBackgroundController
                .GetPhaseBackgroundTexturePath(2),
            async () =>
            {
                Creature cobalt = await CreatureCmd.Add(
                    ModelDb.Monster<LanguageFloorCobaltScar>().ToMutable(),
                    combatState,
                    CombatSide.Enemy,
                    WolfSlot);
                cobalt.PrepareForNextTurn(combatState.PlayerCreatures);
            },
            () => LanguageFloorLiberationBackgroundController
                .SetPhaseBackground(2));
        combatState.SortEnemiesBySlotName();
        TransitionPending = false;
        RefreshLiberationPhaseBgm();
    }

    internal async Task ResolvePhaseTwoDeath(Creature creature)
    {
        if (PhaseComplete
            || TransitionPending
            || CurrentPhase != 2
            || creature.Monster is not LanguageFloorCobaltScar
            || creature.CombatState is not { } combatState)
        {
            return;
        }

        PhaseComplete = false;
        TransitionPending = true;
        CurrentPhase = 3;
        KilledBossCount = Math.Max(KilledBossCount, 2);
        SettlementTriggered = false;
        EndedByLethalDamage = false;
        LanguageFloorLiberationSettlementStore.Record(this);
        LanguageFloorDeathContext.Clear();
        foreach (Creature target in combatState.Creatures.ToArray())
        {
            await PowerCmdCompat.RemoveIfPresent<LanguageFloorScarPower>(
                target);
        }
        await LiberationPhasePlayerRecovery.RestorePlayers(
            combatState,
            PhaseTransitionHealAmount);

        await LiberationPhaseTransition.ShowAsync(
            (ILiberationPhaseBoss)creature.Monster,
            triggerAnimation: true);

        RefreshLiberationPhaseBgm();
    }

    private async Task SpawnPhaseThree(CombatStateLike combatState)
    {
        TextureRect? backgroundImage =
            LanguageFloorLiberationBackgroundController
                .GetCurrentBackgroundImage();
        await LiberationPhaseCleanup.RemovePhaseCreatures(combatState);
        if (combatState.Enemies.Any(static enemy => enemy.IsAlive))
        {
            return;
        }

        await LorexSceneTransitionController.PlayPhaseBackgroundRevealAsync(
            backgroundImage,
            LanguageFloorLiberationBackgroundController
                .GetPhaseBackgroundTexturePath(3),
            async () =>
            {
                Creature boss = await CreatureCmd.Add(
                    ModelDb.Monster<LanguageFloorSmilingFace>().ToMutable(),
                    combatState,
                    CombatSide.Enemy,
                    WolfSlot);
                boss.PrepareForNextTurn(combatState.PlayerCreatures);
            },
            () => LanguageFloorLiberationBackgroundController
                .SetPhaseBackground(3));
        combatState.SortEnemiesBySlotName();
        TransitionPending = false;
        RefreshLiberationPhaseBgm();
    }

    internal async Task CompletePhaseThree(
        LanguageFloorSmilingFace boss,
        CombatStateLike combatState)
    {
        if (PhaseComplete || CurrentPhase != 3)
        {
            return;
        }

        boss.MarkEncounterComplete();
        CurrentPhase = 4;
        PhaseComplete = false;
        TransitionPending = true;
        KilledBossCount = Math.Max(KilledBossCount, 3);
        SettlementTriggered = false;
        EndedByLethalDamage = false;
        LanguageFloorLiberationSettlementStore.Record(this);
        LanguageFloorDeathContext.Clear();
        await LiberationPhasePlayerRecovery.RestorePlayers(
            combatState,
            PhaseTransitionHealAmount);
        await LiberationPhaseTransition.ShowAsync(
            boss,
            triggerAnimation: true);
        RefreshLiberationPhaseBgm();
    }

    private async Task SpawnPhaseFour(CombatStateLike combatState)
    {
        TextureRect? backgroundImage =
            LanguageFloorLiberationBackgroundController
                .GetCurrentBackgroundImage();
        await LiberationPhaseCleanup.RemovePhaseCreatures(combatState);
        if (combatState.Enemies.Any(static enemy => enemy.IsAlive))
        {
            return;
        }

        await LorexSceneTransitionController.PlayPhaseBackgroundRevealAsync(
            backgroundImage,
            LanguageFloorLiberationBackgroundController
                .GetPhaseBackgroundTexturePath(4),
            async () =>
            {
                Creature leftBat = await CreatureCmd.Add(
                    ModelDb.Monster<LanguageFloorBloodBat>().ToMutable(),
                    combatState,
                    CombatSide.Enemy,
                    DipsiaLeftBatSlot);
                Creature boss = await CreatureCmd.Add(
                    ModelDb.Monster<LanguageFloorDipsia>().ToMutable(),
                    combatState,
                    CombatSide.Enemy,
                    DipsiaBossSlot);
                Creature rightBat = await CreatureCmd.Add(
                    ModelDb.Monster<LanguageFloorBloodBat>().ToMutable(),
                    combatState,
                    CombatSide.Enemy,
                    DipsiaRightBatSlot);
                leftBat.PrepareForNextTurn(combatState.PlayerCreatures);
                boss.PrepareForNextTurn(combatState.PlayerCreatures);
                rightBat.PrepareForNextTurn(combatState.PlayerCreatures);
            },
            () => LanguageFloorLiberationBackgroundController
                .SetPhaseBackground(4));
        combatState.SortEnemiesBySlotName();
        TransitionPending = false;
        RefreshLiberationPhaseBgm();
    }

    internal async Task ResolvePhaseFourDeath(Creature creature)
    {
        if (PhaseComplete
            || CurrentPhase != 4
            || creature.Monster is not LanguageFloorDipsia
            || creature.CombatState is not { } combatState)
        {
            return;
        }

        CurrentPhase = 5;
        PhaseComplete = false;
        TransitionPending = true;
        KilledBossCount = Math.Max(KilledBossCount, 4);
        SettlementTriggered = false;
        EndedByLethalDamage = false;
        LanguageFloorLiberationSettlementStore.Record(this);
        LanguageFloorDeathContext.Clear();
        foreach (Creature bat in combatState.Enemies
                     .Where(static enemy =>
                         enemy.Monster is LanguageFloorBloodBat)
                     .ToArray())
        {
            if (bat.IsAlive)
            {
                await CreatureCmd.Kill(bat, force: true);
            }

            if (combatState.Enemies.Contains(bat))
            {
                await LiberationPhaseCleanup.RemoveTransitionCreature(
                    bat,
                    combatState);
            }
        }
        await LiberationPhasePlayerRecovery.RestorePlayers(
            combatState,
            PhaseTransitionHealAmount);

        await LiberationPhaseTransition.ShowAsync(
            (ILiberationPhaseBoss)creature.Monster,
            triggerAnimation: true);
        RefreshLiberationPhaseBgm();
    }

    private async Task SpawnPhaseFive(CombatStateLike combatState)
    {
        TextureRect? backgroundImage =
            LanguageFloorLiberationBackgroundController
                .GetCurrentBackgroundImage();
        await LiberationPhaseCleanup.RemovePhaseCreatures(combatState);
        if (combatState.Enemies.Any(static enemy => enemy.IsAlive))
        {
            return;
        }

        await LorexSceneTransitionController.PlayPhaseBackgroundRevealAsync(
            backgroundImage,
            LanguageFloorLiberationBackgroundController
                .GetPhaseBackgroundTexturePath(5),
            async () =>
            {
                Creature boss = await CreatureCmd.Add(
                    ModelDb.Monster<LanguageFloorMimicry>().ToMutable(),
                    combatState,
                    CombatSide.Enemy,
                    MimicryBossSlot);
                boss.PrepareForNextTurn(combatState.PlayerCreatures);
            },
            () => LanguageFloorLiberationBackgroundController
                .SetPhaseBackground(5));
        combatState.SortEnemiesBySlotName();
        TransitionPending = false;
        RefreshLiberationPhaseBgm();
    }

    internal async Task ResolvePhaseFiveDeath(Creature creature)
    {
        if (PhaseComplete
            || CurrentPhase != 5
            || creature.Monster is not LanguageFloorMimicry mimicry
            || creature.CombatState is not { })
        {
            return;
        }

        if (mimicry.CanEnterFakeDeath(creature))
        {
            await mimicry.EnterFakeDeathFromDeath();
            return;
        }

        if (mimicry is not
            {
                Form: LanguageFloorMimicryForm.Third,
                IsFakeDead: false
            })
        {
            return;
        }

        CurrentPhase = 5;
        PhaseComplete = true;
        TransitionPending = false;
        KilledBossCount = Math.Max(KilledBossCount, 5);
        SettlementTriggered = true;
        EndedByLethalDamage = false;
        LanguageFloorLiberationSettlementStore.Record(this);
        LanguageFloorDeathContext.Clear();
        RefreshLiberationPhaseBgm();
        if (CombatManager.Instance.IsInProgress)
        {
            bool ended = await CombatManager.Instance.CheckWinCondition();
            if (!ended)
            {
                ScheduleDeferredWinConditionCheck();
            }
        }
    }

    // public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    // {
    //     if (_shouldReducePlayers)
    //     {
    //         foreach (Creature player in participants.Where(static creature => creature is {IsPlayer: true, IsAlive: true}))
    //         {
    //             var reducedHp = Math.Max(
    //                 1,
    //                 (int)Math.Ceiling(
    //                     player.CurrentHp
    //                     * LanguageFloorDeathTrackerPower.PlayerCurrentHpRemainingPercent
    //                     / 100m));
    //             await CreatureCmd.SetCurrentHp(player, reducedHp);
    //         }
    //     }
    // }
}
