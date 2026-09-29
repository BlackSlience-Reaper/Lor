using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.liberation.Language;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.scene_transitions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed partial class NaturalFloorLiberationEncounter : LiberationEncounterBase,
    IEncounterBgmSource,
    IFloorLiberationEncounter, ILiberationPhaseBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.PhaseBased(
        "NaturalFloorLiberationBGM",
        LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
        volumeScale: 0.85f);

    public const int PlannedMaxPhase = 4;
    public const int ImplementedMaxPhase = 5;
    public const string BossSlot = "love_and_hatred";
    public const string RageSlot = "blind_rage";
    public const string HermitSlot = "green_stem_hermit";
    public const string GoldRushSlot = "gold_rush";
    internal static readonly string[] HappinessSlots = ["greed_happiness_left", "greed_happiness_right"];
    public const string TearEdgeSlot = "tear_edge";
    private static readonly string[] SwordSlots = ["despair_sword_1", "despair_sword_2", "despair_sword_3"];
    public const int TransitionHeal = 6;
    public const int FailureHpLossPercent = 60;

    // 自然层死亡结算时，最后一名玩家保留的生命，结算中的后续伤害也使用此值。
    private const int SettlementSurvivalHp = 1;

    private static readonly string[] StaffSlots = ["staff_left", "staff_right"];

    public override RoomType RoomType => RoomType.Boss;

    public override bool ShouldGiveRewards => false;

    public override bool HasScene => true;

    public override float GetCameraScaling() =>
        CurrentPhase == 5 ? NihilCameraScaling : 0.9f;

    public override Vector2 GetCameraOffset() =>
        CurrentPhase == 5 ? NihilCameraOffset : Vector2.Zero;

    public override IReadOnlyList<string> Slots =>
    [
        BossSlot, RageSlot, .. StaffSlots, HermitSlot, .. SwordSlots, TearEdgeSlot,
        GoldRushSlot, .. HappinessSlots, NihilBossSlot, .. NihilGirlSlots, .. NihilStatueSlots
    ];

    protected override bool HasCustomBackground => true;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => "res://images/map/placeholder/natural_floor_liberation_encounter_icon";

    public string LiberationFloorId => LiberationFloorIds.Natural;

    public int CurrentPhase { get; private set; } = 1;

    public bool TransitionPending { get; private set; }

    public int KilledBossCount { get; private set; }

    public bool SettlementTriggered { get; private set; }

    private bool _phaseTwoRageDefeated;
    private bool _phaseTwoHermitDefeated;

    internal bool SecondPhaseResolutionPending => CurrentPhase == 2 && (_phaseTwoRageDefeated || _phaseTwoHermitDefeated);

    internal bool UsesRageTransitionCarrier => !SettlementTriggered
        && (SecondPhaseResolutionPending || CurrentPhase == 3 && TransitionPending);

    public bool IsFullyLiberated => CurrentPhase == 5 ? NihilCompleted : KilledBossCount >= PlannedMaxPhase;

    private Dictionary<string, string> _restoredBossState = [];
    private NaturalFloorLoveAndHatredBoss? _boss;

    internal bool IsFirstPhaseSnakeForm => _boss?.IsSnakeForm == true;

    private List<NaturalFloorWrathMonster> _phaseTwo = [];
    private List<NaturalFloorDespairMonster> _phaseThree = [];
    private List<NaturalFloorPhaseMonster> _phaseFour = [];
    private CombatStateLike? _combatState;
    private bool _transitionInProgress;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<NaturalFloorLoveAndHatredBoss>(),
        ModelDb.Monster<NaturalFloorBlindRageBoss>(),
        ModelDb.Monster<NaturalFloorGreenStemHermit>(),
        ModelDb.Monster<NaturalFloorHermitStaff>(),
        ModelDb.Monster<NaturalFloorTearEdgeBoss>(),
        ModelDb.Monster<NaturalFloorForgottenSword>(),
        ModelDb.Monster<NaturalFloorGoldRushBoss>(),
        ModelDb.Monster<NaturalFloorShiningHappiness>(),
        .. NihilPossibleMonsters()
    ];

    public override IEnumerable<string> ExtraAssetPaths => AllPossibleMonsters.SelectMany(static monster => monster.AssetPaths)
        .Concat(LanguageFloorLiberationEncounter.RolandLiberationBgmTracks)
        .Concat(LorexSceneTransitionAssetPaths.All)
        .Append(NaturalFloorLiberationBackground.HumanTexturePath)
        .Append(NaturalFloorLiberationBackground.SnakeTexturePath)
        .Append(NaturalFloorLiberationBackground.WrathTexturePath)
        .Append(NaturalFloorLiberationBackground.DespairTexturePath)
        .Append(NaturalFloorLiberationBackground.GreedTexturePath)
        .Concat(NaturalFloorNihilTransition.AssetPaths)
        .Append(NaturalFloorLiberationBackground.NihilTexturePath);

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _restoredBossState = new(_restoredBossState);
        _boss = null;
        _phaseTwo = [];
        _phaseThree = [];
        _phaseFour = [];
        _phaseFive = [];
        _nihilLastPlayerRound = -1;
        _nihilReplacingStatues = false;
        _combatState = null;
        _transitionInProgress = false;
    }

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        if (CurrentPhase == 5)
        {
            return CreatePhaseFive();
        }

        if (CurrentPhase == 4 && !TransitionPending)
        {
            return CreatePhaseFour(restoring: true);
        }

        if (CurrentPhase == 3 && !TransitionPending || CurrentPhase == 4 && TransitionPending)
        {
            return CreatePhaseThree(restoring: true);
        }

        if (CurrentPhase == 2 && !TransitionPending || CurrentPhase == 3 && TransitionPending)
        {
            return CreatePhaseTwo(restoring: true);
        }

        var boss = (NaturalFloorLoveAndHatredBoss)ModelDb.Monster<NaturalFloorLoveAndHatredBoss>().ToMutable();
        _boss = boss;
        boss.RestoreFormState(_restoredBossState);
        return [(boss, BossSlot)];
    }

    internal async Task OnFirstPhaseDeath(NaturalFloorLoveAndHatredBoss boss, bool prevented)
    {
        if (prevented || SettlementTriggered || TransitionPending || CurrentPhase != 1 || boss.Creature.CombatState is not { } state)
        {
            return;
        }

        KilledBossCount = 1;
        CurrentPhase = 2;
        TransitionPending = true;
        _combatState = state;
        foreach (Creature player in state.PlayerCreatures)
        {
            if (player.GetPower<NaturalFloorBadGuyPower>() is { } mark)
            {
                await PowerCmd.Remove(mark);
            }
        }

        await LiberationPhasePlayerRecovery.RestorePlayers(state, TransitionHeal);
        await LiberationPhaseTransition.ShowAsync(boss, triggerAnimation: true);
        Log.Info("[NaturalFloorLiberation] Phase 1 -> 2 queued; waiting for REVIVE_AND_EMPOWER.");
    }

    internal async Task CompletePhaseTransition()
    {
        if (!TransitionPending || SettlementTriggered || _transitionInProgress || _combatState is not { } state)
        {
            return;
        }

        _transitionInProgress = true;
        NaturalFloorLiberationBackground? background = null;
        try
        {
            // The flag is reset in finally; node lookups and reveal preparation are presentation
            // and must not skip the phase spawn below on one client.
            background = PresentationGuard.Get(
                NaturalFloorLiberationBackground.GetCurrent,
                "NaturalFloorLiberation background lookup");
            PresentationGuard.Run(() => background?.BeginPhaseReveal(), "NaturalFloorLiberation reveal prepare");
            await LiberationPhaseCleanup.RemovePhaseCreatures(state);
            TextureRect? image = background ?? PresentationGuard.Get(
                () => NCombatRoom.Instance?.Background?.GetNodeOrNull<TextureRect>("Layer_00/A"),
                "NaturalFloorLiberation fallback background lookup");
            await LorexSceneTransitionController.PlayPhaseBackgroundRevealAsync(
                image,
                NaturalFloorLiberationBackground.GetPhaseTexturePath(CurrentPhase),
                async () =>
                {
                    var nextPhase = CurrentPhase switch
                    {
                        4 => CreatePhaseFour(restoring: false),
                        3 => CreatePhaseThree(restoring: false),
                        _ => CreatePhaseTwo(restoring: false)
                    };
                    foreach ((MonsterModel monster, string? slot) in nextPhase)
                    {
                        Creature creature = await CreatureCmd.Add(monster, state, CombatSide.Enemy, slot);
                        creature.PrepareForNextTurn(state.PlayerCreatures);
                    }
                    state.SortEnemiesBySlotName();
                },
                () => background?.SetPhaseBackground(CurrentPhase));
            TransitionPending = false;
            RefreshLiberationPhaseBgm();
            if (CurrentPhase == 2)
            {
                await RefreshStaffMode();
            }

            Log.Info($"[NaturalFloorLiberation] Phase {CurrentPhase} spawned with background reveal; enemies={state.Enemies.Count}.");
        }
        finally
        {
            _transitionInProgress = false;
            if (GodotObject.IsInstanceValid(background))
            {
                background!.EndPhaseReveal();
            }
        }
    }

    internal async Task OnBeforeSideTurnStart(CombatSide side)
    {
        if (CurrentPhase == 5)
        {
            await BeforeNihilTurnStart(side);
            return;
        }

        if (side != CombatSide.Enemy)
        {
            return;
        }

        await ResolveSecondPhaseOutcome();
        if (TransitionPending && !SettlementTriggered && _combatState is { } state
            && !state.Enemies.Any(enemy => enemy.Monster is ILiberationPhaseBoss boss && boss.LiberationPhase + 1 == CurrentPhase))
        {
            await CompletePhaseTransition();
        }
    }

    private IReadOnlyList<(MonsterModel, string?)> CreatePhaseThree(bool restoring)
    {
        _phaseThree = [];
        var result = new List<(MonsterModel, string?)>();
        for (int i = 0; i < 3 && !(restoring && CurrentPhase == 4 && TransitionPending); i++)
        {
            var sword = (NaturalFloorForgottenSword)ModelDb.Monster<NaturalFloorForgottenSword>().ToMutable();
            sword.Configure(i);
            Add(sword, SwordSlots[i], "Sword" + i);
        }
        Add((NaturalFloorTearEdgeBoss)ModelDb.Monster<NaturalFloorTearEdgeBoss>().ToMutable(), TearEdgeSlot, "TearEdge");
        return result;
        void Add(NaturalFloorDespairMonster model, string slot, string key)
        {
            if (restoring)
            {
                model.RestoreState(_restoredBossState.Where(p => p.Key.StartsWith(key + ".", StringComparison.Ordinal))
                .ToDictionary(p => p.Key[(key.Length + 1)..], p => p.Value));
            }

            _phaseThree.Add(model);
            result.Add((model, slot));
        }
    }

    private IReadOnlyList<(MonsterModel, string?)> CreatePhaseTwo(bool restoring)
    {
        _phaseTwo = [];
        var result = new List<(MonsterModel, string?)>();
        Add(ModelDb.Monster<NaturalFloorBlindRageBoss>().ToMutable(), RageSlot, "Rage");
        if (restoring && UsesRageTransitionCarrier)
        {
            return result;
        }

        Add(ModelDb.Monster<NaturalFloorGreenStemHermit>().ToMutable(), HermitSlot, "Hermit");
        for (int i = 0; i < 2; i++)
        {
            if (restoring && _restoredBossState.ContainsKey("PhaseTwoSaved") && !_restoredBossState.ContainsKey($"Staff{i}.Hp"))
            {
                continue;
            }

            var staff = (NaturalFloorHermitStaff)ModelDb.Monster<NaturalFloorHermitStaff>().ToMutable();
            staff.SlotNumber = i;
            Add(staff, StaffSlots[i], "Staff" + i);
        }
        return result;

        void Add(MonsterModel model, string slot, string key)
        {
            var monster = (NaturalFloorWrathMonster)model;
            if (restoring)
            {
                monster.RestoreState(_restoredBossState.Where(pair => pair.Key.StartsWith(key + ".", StringComparison.Ordinal))
                    .ToDictionary(pair => pair.Key[(key.Length + 1)..], pair => pair.Value));
            }

            _phaseTwo.Add(monster);
            result.Add((monster, slot));
        }
    }

    internal async Task EnsureControllerPowers(CombatStateLike state)
    {
        _combatState = state;
        foreach (Creature player in state.PlayerCreatures)
        {
            await PowerCmdCompat.Ensure<NaturalFloorLiberationControllerPower>(player, 1, player, null, silent: true);
        }
    }

    internal bool KeepCombatOpen() => !SettlementTriggered && (CurrentPhase == 5 && NihilBoss?.Creature.IsAlive == true || SecondPhaseResolutionPending
        || _combatState is { } state && LiberationCombatEndGuard.ShouldKeepCombatOpen(state, CurrentPhase, TransitionPending, SettlementTriggered));

    internal bool ShouldKeepPhaseBossAfterDeath(Creature creature) => !SettlementTriggered && (creature.Monster switch
    {
        NaturalFloorLoveAndHatredBoss => CurrentPhase == 1 || CurrentPhase == 2 && TransitionPending,
        NaturalFloorBlindRageBoss => CurrentPhase == 2 || CurrentPhase == 3 && TransitionPending,
        NaturalFloorGreenStemHermit => CurrentPhase == 2,
        NaturalFloorTearEdgeBoss => CurrentPhase == 3 || CurrentPhase == 4 && TransitionPending,
        _ => false
    });

    internal bool SuppressTransition(Creature creature) => TransitionPending
        && (CurrentPhase == 2 && creature.Monster is NaturalFloorLoveAndHatredBoss
            || CurrentPhase == 3 && creature.Monster is NaturalFloorBlindRageBoss
            || CurrentPhase == 4 && creature.Monster is NaturalFloorTearEdgeBoss);

    internal bool SuppressPhaseInteraction(Creature creature) => SuppressTransition(creature)
        || UsesRageTransitionCarrier && creature.Monster is NaturalFloorWrathMonster;

    internal bool ShouldPreventPlayerDeath(Creature creature) =>
        creature.IsPlayer
        && creature.CombatState is { } state
        && state.Encounter == this
        && !state.PlayerCreatures.Any(player => player != creature && player.IsAlive)
        && (SettlementTriggered
            || KilledBossCount >= NaturalFloorLiberationSettlementStore.MinimumKilledBossCount);

    internal async Task OnPreventingDeath(Creature creature)
    {
        if (ShouldPreventPlayerDeath(creature))
        {
            await CreatureCmd.SetCurrentHp(creature, SettlementSurvivalHp);
            if (SettlementTriggered)
            {
                return;
            }

            SettlementTriggered = true;
            TransitionPending = false;
            NaturalFloorDespairVideoController.Stop();
            NaturalFloorNihilTransition.Cleanup();
            NaturalFloorLiberationSettlementStore.Record(this);

            // 先锁定已完成阶段数，再清理本阶段；清理触发的死亡钩子不能增加结算档位。
            if (creature.CombatState is not { } state || !CombatManager.Instance.IsInProgress)
            {
                return;
            }

            foreach (Creature enemy in state.Enemies.ToArray())
            {
                if (!CombatManager.Instance.IsInProgress)
                {
                    break;
                }

                if (enemy.IsAlive)
                {
                    await CreatureCmd.Kill(enemy, force: true);
                }
                else
                {
                    await LiberationPhaseCleanup.RemoveTransitionCreature(enemy, state);
                }
            }

            if (CombatManager.Instance.IsInProgress)
            {
                await CombatManager.Instance.CheckWinCondition();
            }

            return;
        }

        if (SuppressTransition(creature))
        {
            await CreatureCmd.SetCurrentHp(creature, 1);
        }
    }

    internal Creature? Rage => _combatState?.Enemies.FirstOrDefault(static c => c.IsAlive && c.Monster is NaturalFloorBlindRageBoss);

    internal Creature? Hermit => _combatState?.Enemies.FirstOrDefault(static c => c.IsAlive && c.Monster is NaturalFloorGreenStemHermit);

    internal Creature[] LivingPlayers() => _combatState?.PlayerCreatures.Where(static c => c.IsAlive).ToArray() ?? [];

    internal Creature[] LivingMonsters() => _combatState?.Enemies.Where(static c => c.IsAlive).ToArray() ?? [];

    internal Creature[] Staffs() => LivingMonsters().Where(static c => c.Monster is NaturalFloorHermitStaff)
        .OrderBy(static c => ((NaturalFloorHermitStaff)c.Monster!).SlotNumber).ToArray();

    internal IReadOnlyList<Creature> RageTargets() => Staffs().FirstOrDefault() is { } staff ? [staff] : Hermit is { } hermit ? [hermit] : [];

    internal IReadOnlyList<Creature> RageGroupTargets() => LivingPlayers().Concat(LivingMonsters().Where(static c => c.Monster is not NaturalFloorBlindRageBoss)).ToArray();

    internal IReadOnlyList<Creature> HermitGroupTargets() => Rage is { } rage ? [.. LivingPlayers(), rage] : LivingPlayers();

    internal async Task RefreshStaffMode()
    {
        if (SettlementTriggered || TransitionPending || SecondPhaseResolutionPending)
        {
            return;
        }

        bool exists = Staffs().Length > 0;
        // SynchronizeStaffMode / ReplaceIllegalCorrosionMove are synchronized state; only the intent
        // node refresh between them is presentation, so a UI failure cannot skip the hermit's sync.
        if (Rage?.Monster is NaturalFloorBlindRageBoss rage)
        {
            rage.SynchronizeStaffMode(exists);
            rage.ReplaceIllegalCorrosionMove();
            await RefreshIntentNode(rage.Creature);
        }
        if (Hermit?.Monster is NaturalFloorGreenStemHermit hermit)
        {
            hermit.SynchronizeStaffMode(exists);
            await RefreshIntentNode(hermit.Creature);
        }
    }

    private static Task RefreshIntentNode(Creature creature)
    {
        return PresentationGuard.RunAsync(async () =>
        {
            if (creature.GetCreatureNode() is { } node)
            {
                await node.RefreshIntents();
            }
        }, "NaturalFloorLiberation intent refresh");
    }

    internal async Task SummonStaffs()
    {
        if (SettlementTriggered || _combatState is not { } state || Staffs().Length > 0)
        {
            return;
        }

        foreach (NaturalFloorHermitStaff old in _phaseTwo.OfType<NaturalFloorHermitStaff>().ToArray())
        {
            await LiberationPhaseCleanup.RemoveTransitionCreature(old.Creature, state);
            _phaseTwo.Remove(old);
        }
        for (int i = 0; i < 2; i++)
        {
            var model = (NaturalFloorHermitStaff)ModelDb.Monster<NaturalFloorHermitStaff>().ToMutable();
            model.SlotNumber = i;
            _phaseTwo.Add(model);
            Creature staff = await CreatureCmd.Add(model, state, CombatSide.Enemy, StaffSlots[i]);
            staff.PrepareForNextTurn(state.PlayerCreatures);
        }
        await RefreshStaffMode();
    }

    internal async Task OnSecondPhaseDeath(Creature creature, bool prevented)
    {
        if (!prevented && !SettlementTriggered && !TransitionPending && CurrentPhase == 2 && creature.CombatState == _combatState)
        {
            if (creature.Monster is NaturalFloorBlindRageBoss)
            {
                _phaseTwoRageDefeated = true;
            }

            if (creature.Monster is NaturalFloorGreenStemHermit)
            {
                _phaseTwoHermitDefeated = true;
            }

            if (SecondPhaseResolutionPending)
            {
                await ShowRageTransitionCarrier();
            }
        }
    }

    private async Task ShowRageTransitionCarrier()
    {
        if (_combatState is not { } state)
        {
            return;
        }
        // Hide outgoing units immediately without detaching their combat context inside a death batch.
        // The turn-boundary resolver removes them before the carrier performs the phase transition.
        foreach (Creature enemy in state.Enemies)
        {
            if (enemy.Monster is NaturalFloorBlindRageBoss)
            {
                continue;
            }

            if (enemy.GetCreatureNode() is not { } node)
            {
                continue;
            }

            node.Visible = false;
            node.IntentContainer.Visible = false;
            node.ToggleIsInteractable(on: false);
        }
        if (state.Enemies.Select(static enemy => enemy.Monster).OfType<NaturalFloorBlindRageBoss>().FirstOrDefault() is { } rage)
        {
            await LiberationPhaseTransition.ShowAsync(rage, triggerAnimation: true);
        }
    }

    internal Task OnAfterSideTurnEnd() => CurrentPhase == 5
        ? ReplaceBrokenNihilStatues() : ResolveSecondPhaseOutcome();

    private async Task ResolveSecondPhaseOutcome()
    {
        if (SettlementTriggered || TransitionPending || !SecondPhaseResolutionPending || _combatState is not { } state)
        {
            return;
        }

        bool hermitDefeated = _phaseTwoHermitDefeated;
        _phaseTwoRageDefeated = false;
        _phaseTwoHermitDefeated = false;
        // Resolve the whole death batch before choosing the success or failure cost.
        KilledBossCount = 2;
        CurrentPhase = 3;
        TransitionPending = true;
        if (hermitDefeated)
        {
            await LiberationPhasePlayerRecovery.RestorePlayers(state, TransitionHeal);
        }
        else
        {
            foreach (Creature player in LivingPlayers())
            {
                await CreatureCmd.SetCurrentHp(player,
                    Math.Max(1, (int)Math.Ceiling(player.CurrentHp * (100m - FailureHpLossPercent) / 100m)));
            }
        }

        NaturalFloorBlindRageBoss? rage = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<NaturalFloorBlindRageBoss>()
            .FirstOrDefault();
        await LiberationPhaseCleanup.RemovePhaseCreatures(state, except: rage?.Creature);
        _phaseTwo.RemoveAll(monster => monster is not NaturalFloorBlindRageBoss);
        if (rage != null)
        {
            // A defeated carrier must be alive to receive its transition turn.
            await CreatureCmd.SetCurrentHp(rage.Creature, 1);
            await LiberationPhaseTransition.ShowAsync(rage, triggerAnimation: true);
        }

        Log.Info($"[NaturalFloorLiberation] Phase 2 -> 3 queued; hermitDefeated={hermitDefeated}; Blind Rage retained with REVIVE_AND_EMPOWER.");
    }

    internal async Task CompleteThirdPhase()
    {
        if (SettlementTriggered || TransitionPending || CurrentPhase != 3 || _combatState is not { } state)
        {
            return;
        }

        KilledBossCount = 3;
        CurrentPhase = 4;
        TransitionPending = true;
        NaturalFloorDespairVideoController.Stop();
        await LiberationPhasePlayerRecovery.RestorePlayers(state, TransitionHeal);
        NaturalFloorTearEdgeBoss? carrier = state.Enemies.Select(static enemy => enemy.Monster)
            .OfType<NaturalFloorTearEdgeBoss>().FirstOrDefault();
        await LiberationPhaseCleanup.RemovePhaseCreatures(state, except: carrier?.Creature);
        _phaseThree.RemoveAll(static monster => monster is not NaturalFloorTearEdgeBoss);
        if (carrier != null)
        {
            await CreatureCmd.SetCurrentHp(carrier.Creature, 1);
            await LiberationPhaseTransition.ShowAsync(carrier, triggerAnimation: true);
        }
    }

    internal async Task CompleteFourthPhase(bool fromDeathHook = false)
    {
        if (SettlementTriggered || TransitionPending || CurrentPhase != 4 || _combatState is not { } state)
        {
            return;
        }

        SettlementTriggered = true;
        KilledBossCount = 4;
        NaturalFloorLiberationSettlementStore.Record(this);
        await RemoveHappiness(preserveDeathBatch: fromDeathHook);
        if (fromDeathHook)
        {
            // 死亡立即解锁结算；本体在当前动作收尾时检查胜利，保留同批死亡单位的上下文。
            return;
        }

        foreach (Creature enemy in state.Enemies.ToArray())
        {
            if (enemy.IsAlive)
            {
                await CreatureCmd.Kill(enemy, force: true);
            }
            else
            {
                await LiberationPhaseCleanup.RemoveTransitionCreature(enemy, state);
            }
        }
        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }
    }

    internal Creature? GoldRush => _combatState?.Enemies
        .FirstOrDefault(static enemy => enemy.Monster is NaturalFloorGoldRushBoss);

    private IReadOnlyList<(MonsterModel, string?)> CreatePhaseFour(bool restoring)
    {
        _phaseFour = [];
        var result = new List<(MonsterModel, string?)>();
        Add((NaturalFloorGoldRushBoss)ModelDb.Monster<NaturalFloorGoldRushBoss>().ToMutable(), GoldRushSlot, "GoldRush");
        if (restoring)
        {
            for (int i = 0; i < HappinessSlots.Length; i++)
            {
                string key = "Happiness" + i;
                if (!_restoredBossState.TryGetValue(key + ".Hp", out string? hp) || !int.TryParse(hp, out int value) || value <= 0)
                {
                    continue;
                }

                var happiness = (NaturalFloorShiningHappiness)ModelDb.Monster<NaturalFloorShiningHappiness>().ToMutable();
                happiness.SlotNumber = i;
                Add(happiness, HappinessSlots[i], key);
            }
        }
        return result;

        void Add(NaturalFloorPhaseMonster monster, string slot, string key)
        {
            if (restoring)
            {
                monster.RestoreState(_restoredBossState.Where(entry => entry.Key.StartsWith(key + ".", StringComparison.Ordinal))
                    .ToDictionary(entry => entry.Key[(key.Length + 1)..], entry => entry.Value));
            }

            _phaseFour.Add(monster);
            result.Add((monster, slot));
        }
    }

    internal async Task SummonHappiness()
    {
        if (SettlementTriggered || TransitionPending || CurrentPhase != 4 || _combatState is not { } state)
        {
            return;
        }

        for (int i = 0; i < HappinessSlots.Length; i++)
        {
            if (state.Enemies.Any(enemy => enemy.IsAlive && enemy.SlotName == HappinessSlots[i]))
            {
                continue;
            }

            foreach (NaturalFloorShiningHappiness old in _phaseFour.OfType<NaturalFloorShiningHappiness>()
                         .Where(monster => monster.SlotNumber == i).ToArray())
            {
                await LiberationPhaseCleanup.RemoveTransitionCreature(old.Creature, state);
                _phaseFour.Remove(old);
            }

            var model = (NaturalFloorShiningHappiness)ModelDb.Monster<NaturalFloorShiningHappiness>().ToMutable();
            model.SlotNumber = i;
            _phaseFour.Add(model);
            NaturalFloorGoldRushBoss.Sound("summon");
            Creature happiness = await CreatureCmd.Add(model, state, CombatSide.Enemy, HappinessSlots[i]);
            happiness.PrepareForNextTurn(state.PlayerCreatures);
        }
        state.SortEnemiesBySlotName();
    }

    internal async Task RemoveHappiness(bool preserveDeathBatch = false)
    {
        if (_combatState is not { } state)
        {
            return;
        }

        foreach (Creature happiness in state.Enemies.Where(static enemy => enemy.Monster is NaturalFloorShiningHappiness).ToArray())
        {
            var model = (NaturalFloorShiningHappiness)happiness.Monster!;
            model.SuppressDeathReward = true;
            if (preserveDeathBatch && happiness.IsDead)
            {
                continue;
            }

            if (happiness.GetPower<NaturalFloorShiningHappinessPower>() is { } power)
            {
                await PowerCmd.Remove(power);
            }

            await LiberationPhaseCleanup.RemoveTransitionCreature(happiness, state);
            _phaseFour.Remove(model);
        }
    }

    // Called between phase state writes (e.g. before RefreshStaffMode); audio is local presentation.
    public override void RefreshLiberationPhaseBgm() =>
        PresentationGuard.Run(EncounterBgmController.RefreshCurrentEncounterTrack, "NaturalFloorLiberation bgm");

    public override Dictionary<string, string> SaveCustomState()
    {
        // 终战只保存入口和结算结果，重进时从初始阵容重新开始。
        if (CurrentPhase == 5)
        {
            return new Dictionary<string, string>
            {
                ["CurrentPhase"] = CurrentPhase.ToString(),
                ["EntryBranchChosen"] = _entryBranchChosen.ToString(),
                ["KilledBossCount"] = KilledBossCount.ToString(),
                ["SettlementTriggered"] = SettlementTriggered.ToString(),
                ["NihilCompleted"] = NihilCompleted.ToString()
            };
        }

        var state = _boss?.SaveFormState()
            ?? new Dictionary<string, string>(_restoredBossState);
        state["KilledBossCount"] = KilledBossCount.ToString();
        state["SettlementTriggered"] = SettlementTriggered.ToString();
        state["CurrentPhase"] = CurrentPhase.ToString();
        state["TransitionPending"] = TransitionPending.ToString();
        state["PhaseTwoRageDefeated"] = _phaseTwoRageDefeated.ToString();
        state["PhaseTwoHermitDefeated"] = _phaseTwoHermitDefeated.ToString();
        if (CurrentPhase == 2 && !TransitionPending || CurrentPhase == 3 && TransitionPending)
        {
            foreach (string key in state.Keys.Where(static key => key.StartsWith("Staff", StringComparison.Ordinal)
                         || key.StartsWith("Hermit.", StringComparison.Ordinal) || key.StartsWith("Rage.", StringComparison.Ordinal)).ToArray())
            {
                state.Remove(key);
            }

            state["PhaseTwoSaved"] = "True";
            foreach (NaturalFloorWrathMonster monster in _phaseTwo.Where(static m => m.Creature is { IsAlive: true }
                         || m is NaturalFloorBlindRageBoss or NaturalFloorGreenStemHermit))
            {
                string key = monster switch
                {
                    NaturalFloorBlindRageBoss => "Rage",
                    NaturalFloorGreenStemHermit => "Hermit",
                    NaturalFloorHermitStaff staff => "Staff" + staff.SlotNumber,
                    _ => throw new InvalidOperationException()
                };
                foreach (var entry in monster.CaptureState())
                {
                    state[key + "." + entry.Key] = entry.Value;
                }
            }
        }
        if (CurrentPhase == 3 && !TransitionPending || CurrentPhase == 4 && TransitionPending)
        {
            foreach (NaturalFloorDespairMonster monster in _phaseThree)
            {
                string key = monster is NaturalFloorForgottenSword sword ? "Sword" + sword.SwordIndex : "TearEdge";
                foreach (var entry in monster.CaptureState())
                {
                    state[key + "." + entry.Key] = entry.Value;
                }
            }
        }

        if (CurrentPhase == 4 && !TransitionPending)
        {
            foreach (string key in state.Keys.Where(static key => key.StartsWith("GoldRush.", StringComparison.Ordinal)
                         || key.StartsWith("Happiness", StringComparison.Ordinal)).ToArray())
            {
                state.Remove(key);
            }

            foreach (NaturalFloorPhaseMonster monster in _phaseFour.Where(static monster => monster.Creature.IsAlive
                         || monster is NaturalFloorGoldRushBoss))
            {
                string key = monster is NaturalFloorShiningHappiness happiness ? "Happiness" + happiness.SlotNumber : "GoldRush";
                foreach (var entry in monster.CaptureState())
                {
                    state[key + "." + entry.Key] = entry.Value;
                }
            }
        }

        state["EntryBranchChosen"] = _entryBranchChosen.ToString();
        state["NihilCompleted"] = NihilCompleted.ToString();
        return state;
    }

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        _restoredBossState = new(state);
        var bag = new EncounterStateBag(state);
        // 空状态与战前快照仍需检查遗物；旧版已开始的战斗保留原阶段。
        if (bag.TryReadBool("EntryBranchChosen", out bool chosen))
        {
            _entryBranchChosen = chosen;
        }
        else
        {
            _entryBranchChosen = state.ContainsKey("BossHp")
                || bag.ReadInt("CurrentPhase", 1) > 1
                || bag.ReadInt("KilledBossCount", 0) > 0
                || bag.ReadBool("TransitionPending")
                || bag.ReadBool("SettlementTriggered");
        }
        NihilCompleted = bag.ReadBool("NihilCompleted");
        CurrentPhase = bag.ReadClampedInt("CurrentPhase", 1, 1, ImplementedMaxPhase);
        TransitionPending = bag.ReadBool("TransitionPending");
        _phaseTwoRageDefeated = bag.ReadBool("PhaseTwoRageDefeated");
        _phaseTwoHermitDefeated = bag.ReadBool("PhaseTwoHermitDefeated");
        KilledBossCount = bag.ReadClampedInt("KilledBossCount", 0, 0, PlannedMaxPhase);
        SettlementTriggered = bag.ReadBool("SettlementTriggered");
        if (CurrentPhase == 5)
        {
            // 兼容旧存档：丢弃终战怪物快照，仅保留上方已读取的入口和结算信息。
            _restoredBossState.Clear();
            TransitionPending = false;
        }
    }
}
