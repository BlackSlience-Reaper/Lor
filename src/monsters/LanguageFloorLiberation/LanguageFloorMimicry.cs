using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryLib.Models;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.LanguageFloorLiberation;
using LibraryOfRuina.cards.LanguageFloorLiberation;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.visuals.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

public enum LanguageFloorMimicryForm
{
    First,
    Second,
    Third
}

internal enum LanguageFloorMimicryMove
{
    ClumsyFlesh,
    EyeContact,
    ExtendArm,
    HardCocoon,
    Mimic,
    Skin,
    Imitate,
    Hello,
    Goodbye
}

public sealed class LanguageFloorMimicry :
    LorMonsterModel,
    ILiberationPrimaryPhaseBoss
{
    public const int FormOneMaxHpBase = 750;
    public const int FormTwoMaxHpBase = 750;
    public const int FormThreeMaxHpBase = 800;
    public const int FormOneMaxChao = 360;
    public const int FormTwoMaxChao = 400;
    public const int FormThreeMaxChao = 600;
    public const int FormTwoEntryHpPercent = 50;
    public const int FormTwoThorns = 16;
    public const int FormTwoBlock = 99;
    public const int FormTwoInitialPlating = 50;
    public const int FormTwoActionsBeforeEvolution = 3;
    public const int FormThreeMinimumHpPercent = 70;
    public const int HardenThreshold = 13;
    public const int FormThreeRegenerationDamageThreshold = 100;
    public const int FormThreeRegenerationPercent = 10;
    public const int MaximumMimicStacks = 20;
    public const int MimicDecayPerTurn = 1;

    public const int ClumsyFleshHits = 3;
    public const int ClumsyFleshFlaw = 5;
    public const int ClumsyFleshFlawTurns = 3;
    public const int FearCards = 1;
    public const int ExtendArmWeak = 5;
    public const int ExtendArmWeakTurns = 3;
    public const int MimicHits = 2;
    public const int MimicStrength = 3;
    public const int SkinHits = 3;
    public const int SkinBlock = 24;
    public const int SkinMimicCost = 5;
    public const int SkinStrong = 5;
    public const int ImitateBurns = 6;
    public const int ImitateConfusion = 1;
    public const int ImitateMimicCost = 10;
    public const int HelloMimicCost = 8;
    public const int HelloVulnerable = 5;
    public const int GoodbyeBleed = 10;
    public const int GoodbyeMimicCost = 20;
    public const int EnhancedMultiplier = 3;

    public const string ClumsyFleshMoveId = "CLUMSY_FLESH";
    public const string EyeContactMoveId = "EYE_CONTACT";
    public const string ExtendArmMoveId = "EXTEND_ARM";
    public const string HardCocoonMoveId = "HARD_COCOON";
    public const string MimicMoveId = "MIMIC";
    public const string SkinMoveId = "SKIN";
    public const string ImitateMoveId = "IMITATE";
    public const string HelloMoveId = "HELLO";
    public const string GoodbyeMoveId = "GOODBYE";
    public const string EvolutionMoveId = "EVOLUTION";
    public const string ReviveAndEmpowerMoveId = EvolutionMoveId;
    private const string RouterMoveId = "LANGUAGE_FLOOR_MIMICRY_ROUTER";

    public const string TextureRoot =
        "res://images/monsters/language_floor_liberation/mimicry/";
    public const string FormOneIdleTexturePath =
        TextureRoot + "form1_idle.png";
    public const string FormOneThrustTexturePath =
        TextureRoot + "form1_attack_thrust.png";
    public const string FormOneHitTexturePath =
        TextureRoot + "form1_hit.png";
    public const string FormTwoIdleTexturePath =
        TextureRoot + "form2_idle.png";
    public const string FormThreeIdleTexturePath =
        TextureRoot + "form3_idle.png";
    public const string FormThreeStrikeTexturePath =
        TextureRoot + "form3_attack_strike.png";
    public const string FormThreeThrustTexturePath =
        TextureRoot + "form3_attack_thrust.png";
    public const string FormThreeSlashTexturePath =
        TextureRoot + "form3_attack_slash.png";
    public const string FormThreeHitTexturePath =
        TextureRoot + "form3_hit.png";
    public const string FormThreeParryTexturePath =
        TextureRoot + "form3_parry.png";
    public const string FormThreeHelloTexturePath =
        TextureRoot + "form3_hello.png";
    public const string FormThreeGoodbyeTexturePath =
        TextureRoot + "form3_goodbye.png";

    private const string SfxRoot =
        "res://audio/sfx/language_floor_liberation/mimicry/";
    public const string ChangeSfxPath = SfxRoot + "change.ogg";
    public const string GoodbyeSfxPath = SfxRoot + "goodbye.ogg";
    public const string GuardSfxPath = SfxRoot + "guard.ogg";
    public const string HelloSfxPath = SfxRoot + "hello.ogg";
    public const string NormalFleshSfxPath =
        SfxRoot + "normal_flesh.ogg";
    public const string SkinSfxPath = SfxRoot + "skin.ogg";
    public const string StrongFleshSfxPath =
        SfxRoot + "strong_flesh.ogg";
    public const string StrongHorizontalSfxPath =
        SfxRoot + "strong_hori.ogg";
    public const string StrongVerticalSfxPath =
        SfxRoot + "strong_vert.ogg";
    public const string GoodbyeAttackSfxPath =
        SfxRoot + "goodbye_attack.ogg";
    public const string GoodbyeBloodSfxPath =
        SfxRoot + "goodbye_blood.ogg";

    public const string MimicPowerIconPath =
        "res://images/powers/language_floor_mimicry_mimic_power.png";
    public const string FearCardPortraitPath =
        "res://images/packed/card_portraits/status/language_floor_fear_card.png";

    private const float AttackSegmentSeconds = 0.45f;

    private static readonly (LanguageFloorMimicryMove Move, int Weight)[]
        FormOneWeightedMoves =
        [
            (LanguageFloorMimicryMove.ClumsyFlesh, 2),
            (LanguageFloorMimicryMove.EyeContact, 1),
            (LanguageFloorMimicryMove.ExtendArm, 1)
        ];

    private static readonly (LanguageFloorMimicryMove Move, int Weight)[]
        FormThreeWeightedMoves =
        [
            (LanguageFloorMimicryMove.Mimic, 3),
            (LanguageFloorMimicryMove.Skin, 2),
            (LanguageFloorMimicryMove.Imitate, 2),
            (LanguageFloorMimicryMove.Hello, 1),
            (LanguageFloorMimicryMove.Goodbye, 1)
        ];

    public static readonly string[] AssetPathsStatic =
        LanguageFloorMimicryCreatureVisuals.Profile.AssetPaths
            .Concat(
            [
                ChangeSfxPath,
                GoodbyeSfxPath,
                GuardSfxPath,
                HelloSfxPath,
                NormalFleshSfxPath,
                SkinSfxPath,
                StrongFleshSfxPath,
                StrongHorizontalSfxPath,
                StrongVerticalSfxPath,
                GoodbyeAttackSfxPath,
                GoodbyeBloodSfxPath,
                MimicPowerIconPath,
                FearCardPortraitPath
            ])
            .ToArray();

    public LanguageFloorMimicryForm Form { get; private set; } =
        LanguageFloorMimicryForm.First;

    public bool Initialized { get; private set; }

    public int FormOneMaxHp { get; private set; }

    public int FormTwoMaxHp { get; private set; }

    public int FormThreeMaxHp { get; private set; }

    public int FormTwoActionsCompleted { get; private set; }

    public int PreviousMove { get; private set; } = -1;

    public bool EvolutionPending { get; private set; }

    public int PendingForm { get; private set; } = -1;

    public int MimicStacks { get; private set; }

    public int RoundDamageTaken { get; private set; }

    public bool PlannedMoveEnhanced { get; private set; }

    public int PlannedTargetCombatId { get; private set; }

    public bool SkipCurrentFormTwoEnemyEnd { get; private set; }

    public bool SkipCurrentFormThreeEnemyEndRecovery { get; private set; }

    private MoveState? _evolutionState;
    private bool _isTransitioning;
    private List<string> _lastAttackAnimationTriggers = [];
    private List<uint?> _lastAttackTargetCombatIds = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _lastAttackAnimationTriggers = [.. _lastAttackAnimationTriggers];
        _lastAttackTargetCombatIds = [.. _lastAttackTargetCombatIds];
        _evolutionState = null;
    }

    public bool IsFakeDead => EvolutionPending;

    // 假死后的进化必须始终是当前意图；被击晕、混乱锁或其他强制切招覆盖时需要重新锁定。
    private bool IsEvolutionIntentActive =>
        NextMove.Id == EvolutionMoveId;

    internal IReadOnlyList<string> LastAttackAnimationTriggers =>
        _lastAttackAnimationTriggers;

    internal IReadOnlyList<uint?> LastAttackTargetCombatIds =>
        _lastAttackTargetCombatIds;

    public int LiberationPhase => 5;

    internal int ScaledFormThreeRegenerationDamageThreshold =>
        (int)Math.Ceiling(
            MultiplayerScalingPatchHelper.ScaleHpAmount(
                Creature.CombatState,
                this,
                FormThreeRegenerationDamageThreshold));

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp => FormOneMaxHpBase;

    public override int MaxInitialHp => FormOneMaxHpBase;

    public override int DefaultChaoResistance => FormOneMaxChao;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData =>
            Form switch
            {
                LanguageFloorMimicryForm.Second =>
                    CreateFormTwoResistance(),
                LanguageFloorMimicryForm.Third =>
                    CreateFormThreePhysicalResistance(),
                _ => null
            };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData =>
            Form switch
            {
                LanguageFloorMimicryForm.Second =>
                    CreateFormTwoResistance(),
                LanguageFloorMimicryForm.Third =>
                    CreateImmuneResistance(),
                _ => null
            };

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(EnumerateIntentAssets().SelectMany(static intent =>
                intent.AssetPaths))
            .Distinct();

    private int ClumsyFleshDamage => DeadlyDamage(8, 10);

    private int EyeContactDamage => DeadlyDamage(20, 25);

    private int ExtendArmDamage => DeadlyDamage(15, 18);

    private int MimicDamage => DeadlyDamage(12, 17);

    private int SkinDamage => DeadlyDamage(9, 12);

    private int HelloDamage => DeadlyDamage(30, 39);

    private int GoodbyeDamage => DeadlyDamage(40, 45);

    private int ImitateBurnCount =>
        PlannedMoveEnhanced
            ? ImitateBurns * 2
            : ImitateBurns;

    private int ImitateConfusionAmount =>
        PlannedMoveEnhanced
            ? ImitateConfusion * 2
            : ImitateConfusion;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        PresentationGuard.Run(
            () => LanguageFloorLiberationBackgroundController.SetPhaseBackground(5),
            "LanguageFloorMimicry phase background");
        if (!Initialized)
        {
            Initialized = true;
            Form = LanguageFloorMimicryForm.First;
            FormOneMaxHp = Creature.MaxHp;
            if (Creature is LibraryCreature libraryCreature)
            {
                await MultiplayerScalingPatchHelper.SetMonsterBaseMaxAndCurrentChaoValue(
                    libraryCreature,
                    FormOneMaxChao);
            }
        }

        await RefreshFormPowers();
        await ApplyFormResistances(Form);
        await ApplyVisualState();
        if (EvolutionPending)
        {
            ForceReviveAndEmpowerState();
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (EvolutionPending)
        {
            await RecoverPendingEvolution(side);
        }

        if (side == CombatSide.Player
            && Form == LanguageFloorMimicryForm.Third)
        {
            RoundDamageTaken = 0;
        }

        if (side == CombatSide.Enemy
            && Form == LanguageFloorMimicryForm.Second
            && !EvolutionPending
            && Creature.IsAlive)
        {
            await EnsureFormTwoThorns();
            if (FormTwoActionsCompleted >= 1)
            {
                await CreatureCmd.Heal(
                    Creature,
                    Math.Ceiling(Creature.MaxHp * 0.5m));
            }
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }

    protected override async Task AfterSideTurnEndInternal(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEndInternal(
            choiceContext,
            side,
            participants);
        if (side != CombatSide.Enemy || EvolutionPending || Creature.IsDead)
        {
            return;
        }

        if (Form == LanguageFloorMimicryForm.Second)
        {
            if (SkipCurrentFormTwoEnemyEnd)
            {
                SkipCurrentFormTwoEnemyEnd = false;
                return;
            }

            FormTwoActionsCompleted++;
            if (FormTwoActionsCompleted
                >= FormTwoActionsBeforeEvolution)
            {
                decimal remainingRatio = Creature.MaxHp <= 0
                    ? 0m
                    : Creature.CurrentHp / (decimal)Creature.MaxHp;
                await TransitionToThird(
                    remainingRatio,
                    skipCurrentEnemyEndRecovery: false);
            }

            return;
        }

        if (Form != LanguageFloorMimicryForm.Third)
        {
            return;
        }

        if (SkipCurrentFormThreeEnemyEndRecovery)
        {
            SkipCurrentFormThreeEnemyEndRecovery = false;
        }
        else if (RoundDamageTaken
                 < ScaledFormThreeRegenerationDamageThreshold)
        {
            await CreatureCmd.Heal(
                Creature,
                Math.Ceiling(
                    Creature.MaxHp
                    * FormThreeRegenerationPercent
                    / 100m));
        }

        if (MimicStacks > 0)
        {
            MimicStacks = Math.Max(
                0,
                MimicStacks - MimicDecayPerTurn);
            await SyncMimicPower();
        }
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        await base.AfterDamageReceived(
            choiceContext,
            target,
            result,
            props,
            dealer,
            cardSource);
        if (target != Creature
            || Form != LanguageFloorMimicryForm.Third
            || result.UnblockedDamage <= 0)
        {
            return;
        }

        RoundDamageTaken += result.UnblockedDamage;
        if (ValuePropCompat.IsPoweredAttack(props))
        {
            await GainMimic(1);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var clumsyFlesh = new MoveState(
            ClumsyFleshMoveId,
            ClumsyFleshMove,
            new BadgedAttackIntent(
                () => ClumsyFleshDamage,
                () => ClumsyFleshHits,
                "LANGUAGE_FLOOR_MIMICRY_CLUMSY_FLESH.description",
                IntentBadge.Flaw(
                    ClumsyFleshFlaw,
                    ClumsyFleshFlawTurns)));
        var eyeContact = new MoveState(
            EyeContactMoveId,
            EyeContactMove,
            new BadgedAttackIntent(
                () => EyeContactDamage,
                () => 1,
                "LANGUAGE_FLOOR_MIMICRY_EYE_CONTACT.description",
                IntentBadge.StatusCard<LanguageFloorFearCard>(
                    FearCards)));
        var extendArm = new MoveState(
            ExtendArmMoveId,
            ExtendArmMove,
            new BadgedAttackIntent(
                () => ExtendArmDamage,
                () => 1,
                "LANGUAGE_FLOOR_MIMICRY_EXTEND_ARM.description",
                IntentBadge.FromPower<LibraryWeakPower>(
                    ExtendArmWeak,
                    ExtendArmWeakTurns.ToString(),
                    ExtendArmWeak.ToString()),
                IntentBadge.StatusCard<LanguageFloorFearCard>(
                    FearCards)));
        var hardCocoon = new MoveState(
            HardCocoonMoveId,
            HardCocoonMove,
            new DefendIntent());
        var mimic = new MoveState(
            MimicMoveId,
            MimicMove,
            new BadgedAttackIntent(
                () => MimicDamage,
                () => MimicHits,
                "LANGUAGE_FLOOR_MIMICRY_MIMIC.description",
                IntentBadge.Strength(MimicStrength)));
        var skin = new MoveState(
            SkinMoveId,
            SkinMove,
            new BadgedAttackIntent(
                () => SkinDamage,
                () => SkinHits,
                "LANGUAGE_FLOOR_MIMICRY_SKIN.description",
                IntentBadge.FromPower<LibraryStrongPower>(
                    () => PlannedMoveEnhanced
                        ? SkinStrong
                        : 0)));
        var imitate = new MoveState(
            ImitateMoveId,
            ImitateMove,
            new DynamicDetailedStatusCardIntent<Burn>(
                () => ImitateBurnCount,
                PileType.Draw),
            new BadgedDebuffIntent(
                IntentBadge.FromPower<LibraryOfRuinaConfusionPower>(
                    () => ImitateConfusionAmount),
                ImitateConfusion,
                "LANGUAGE_FLOOR_MIMICRY_IMITATE.description"));
        var hello = new MoveState(
            HelloMoveId,
            HelloMove,
            new BadgedAttackIntent(
                () => HelloDamage,
                () => 1,
                "LANGUAGE_FLOOR_MIMICRY_HELLO.description",
                IntentBadge.FromPower<LibraryVulnerablePower>(
                    () => PlannedMoveEnhanced
                        ? HelloVulnerable
                        : 0)));
        var goodbye = new MoveState(
            GoodbyeMoveId,
            GoodbyeMove,
            new BadgedAttackIntent(
                () => PlannedMoveEnhanced
                    ? GoodbyeDamage * EnhancedMultiplier
                    : GoodbyeDamage,
                () => 1,
                "LANGUAGE_FLOOR_MIMICRY_GOODBYE.description",
                IntentBadge.FromPower<LibraryBleedingPower>(
                    () => PlannedMoveEnhanced
                        ? GoodbyeBleed * EnhancedMultiplier
                        : GoodbyeBleed)));
        // 假死后的进化必须越过混乱锁，避免复活行动被击晕状态阻断。
        _evolutionState = LiberationPhaseBossMoves.CreateState(
            EvolutionMoveId,
            EvolutionMove);

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, _) => ResolvePlannedMoveId(RunRng.MonsterAi));
        foreach (MoveState state in new[]
                 {
                     clumsyFlesh,
                     eyeContact,
                     extendArm,
                     hardCocoon,
                     mimic,
                     skin,
                     imitate,
                     hello,
                     goodbye,
                     _evolutionState
                 })
        {
            state.FollowUpState = router;
        }

        return new MonsterMoveStateMachine(
            [
                clumsyFlesh,
                eyeContact,
                extendArm,
                hardCocoon,
                mimic,
                skin,
                imitate,
                hello,
                goodbye,
                _evolutionState,
                router
            ],
            EvolutionPending ? _evolutionState : router);
    }

    public bool CanEnterFakeDeath(Creature creature) =>
        creature == Creature
        && !EvolutionPending
        && Form != LanguageFloorMimicryForm.Third
        && Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter
            {
                CurrentPhase: 5,
                PhaseComplete: false
            };

    public async Task EnterFakeDeathFromDeath()
    {
        if (!CanEnterFakeDeath(Creature))
        {
            return;
        }

        EvolutionPending = true;
        PendingForm = (int)NextFormAfter(Form);
        PlannedMoveEnhanced = false;
        PlannedTargetCombatId = 0;
        await FakeDeathDebuffHelper.ClearDebuffs(Creature);
        ForceReviveAndEmpowerState();
    }

    // 假死后的进化目标：第一形态进入第二形态，其余情况进入第三形态。
    private static LanguageFloorMimicryForm NextFormAfter(
        LanguageFloorMimicryForm form) =>
        form == LanguageFloorMimicryForm.First
            ? LanguageFloorMimicryForm.Second
            : LanguageFloorMimicryForm.Third;

    public async Task TriggerReviveAndEmpowerState()
    {
        await LiberationPhaseBossMoves.TriggerHitAnimationIfVisible(Creature);
        ForceReviveAndEmpowerState();
    }

    public void ForceReviveAndEmpowerState()
    {
        LiberationPhaseBossMoves.ForceState(this, _evolutionState);
    }

    // 假死后如果进化意图被击晕、混乱锁等外部切招覆盖，拟态会一直停留在假死状态、既不能被选中也不会进化。
    // 每个阵营回合开始时重新锁定进化意图；敌方回合仍无法锁定时直接完成进化，保证形态推进不依赖状态机。
    private async Task RecoverPendingEvolution(CombatSide side)
    {
        if (!EvolutionPending || IsEvolutionIntentActive)
        {
            return;
        }

        Log.Warn(
            "[LanguageFloorMimicry] Pending evolution intent was replaced by "
            + NextMove.Id
            + "; restoring the evolution intent.");
        ForceReviveAndEmpowerState();
        if (IsEvolutionIntentActive || side != CombatSide.Enemy)
        {
            return;
        }

        Log.Warn(
            "[LanguageFloorMimicry] Evolution intent could not be restored; "
            + "evolving immediately at enemy turn start.");
        await EvolutionMove([]);
    }

    private async Task EvolutionMove(IReadOnlyList<Creature> targets)
    {
        if (!EvolutionPending)
        {
            return;
        }

        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        // 待进化形态缺失或无效时按当前形态推进，假死不能因为存档状态不完整而永远无法结束。
        LanguageFloorMimicryForm target = PendingForm switch
        {
            (int)LanguageFloorMimicryForm.Second =>
                LanguageFloorMimicryForm.Second,
            (int)LanguageFloorMimicryForm.Third =>
                LanguageFloorMimicryForm.Third,
            _ => NextFormAfter(Form)
        };
        if (target == LanguageFloorMimicryForm.Second)
        {
            await TransitionToSecond();
            return;
        }

        decimal remainingRatio = Creature.MaxHp <= 0
            ? 0m
            : Creature.CurrentHp / (decimal)Creature.MaxHp;
        await TransitionToThird(
            remainingRatio,
            skipCurrentEnemyEndRecovery: true);
    }

    private async Task ClumsyFleshMove(
        IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> players = PlannedAttackTargets();
        await ExecuteAttack(
            ClumsyFleshDamage,
            ClumsyFleshHits,
            players,
            formOne: true);
        await LibraryPowerCmd
            .Apply<LibraryDisarmPower>(
                new ThrowingPlayerChoiceContext(),
                players.Where(static player => player.IsAlive),
                ClumsyFleshFlaw,
                ClumsyFleshFlawTurns,
                IsPermanent: false,
                Creature,
                null);
    }

    private async Task EyeContactMove(
        IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> players = PlannedAttackTargets();
        await ExecuteAttack(
            EyeContactDamage,
            hits: 1,
            players,
            formOne: true);
        await AddFearToTargets(players);
    }

    private async Task ExtendArmMove(
        IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> players = PlannedAttackTargets();
        await ExecuteAttack(
            ExtendArmDamage,
            hits: 1,
            players,
            formOne: true);
        await LibraryPowerCmd
            .Apply<LibraryWeakPower>(
                new ThrowingPlayerChoiceContext(),
                players.Where(static player => player.IsAlive),
                ExtendArmWeak,
                ExtendArmWeakTurns,
                IsPermanent: false,
                Creature,
                null);
        await AddFearToTargets(players);
    }

    private async Task HardCocoonMove(
        IReadOnlyList<Creature> targets)
    {
        PlaySfx(GuardSfxPath);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Parry",
            AttackSegmentSeconds);
        await CreatureCmd.GainBlock(
            Creature,
            FormTwoBlock,
            ValueProp.Move,
            null);
    }

    private async Task MimicMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttack(
            MimicDamage,
            MimicHits,
            PlannedAttackTargets());
        await PowerCmdCompat.Apply<StrengthPower>(
            Creature,
            MimicStrength,
            Creature,
            null);
    }

    private async Task SkinMove(IReadOnlyList<Creature> targets)
    {
        if (PlannedMoveEnhanced)
        {
            await LibraryPowerCmd
                .Apply<LibraryStrongPower>(
                    new ThrowingPlayerChoiceContext(),
                    Creature,
                    SkinStrong,
                    0,
                    IsPermanent: false,
                    Creature,
                    null);
        }

        PlaySfx(SkinSfxPath);
        await ExecuteAttack(
            SkinDamage,
            SkinHits,
            PlannedAttackTargets(),
            playPerHitSfx: false);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Parry",
            AttackSegmentSeconds);
        await CreatureCmd.GainBlock(
            Creature,
            SkinBlock,
            ValueProp.Move,
            null);
    }

    private async Task ImitateMove(
        IReadOnlyList<Creature> targets)
    {
        int burns = PlannedMoveEnhanced
            ? ImitateBurns * 2
            : ImitateBurns;
        int confusion = PlannedMoveEnhanced
            ? ImitateConfusion * 2
            : ImitateConfusion;
        PlaySfx(GuardSfxPath);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Parry",
            AttackSegmentSeconds);
        foreach (Creature player in LivingPlayers())
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Burn>(
                player,
                PileType.Draw,
                burns,
                addedByPlayer: false,
                CardPilePosition.Random);
            await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
                player,
                confusion,
                Creature,
                null);
        }
    }

    private async Task HelloMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> players = PlannedAttackTargets();
        PlaySfx(HelloSfxPath);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Hello",
            AttackSegmentSeconds);
        await DamageAllPlayers(HelloDamage);
        if (!PlannedMoveEnhanced)
        {
            return;
        }

        foreach (Creature player in players.Where(
                     static player => player.IsAlive))
        {
            await LibraryPowerCmd
                .Apply<LibraryVulnerablePower>(
                    player,
                    HelloVulnerable,
                    turns: -1,
                    Creature,
                    null);
        }
    }

    private async Task GoodbyeMove(
        IReadOnlyList<Creature> targets)
    {
        int multiplier = PlannedMoveEnhanced
            ? EnhancedMultiplier
            : 1;
        IReadOnlyList<Creature> players = PlannedAttackTargets();
        await PresentationGuard.RunAsync(
            () => LanguageFloorMimicrySpecialEffects.PlayGoodbyeAsync(Creature, players),
            "LanguageFloorMimicry goodbye effect");
        await DamageAllPlayers(GoodbyeDamage * multiplier);
        await PowerCmdCompat.Apply<LibraryBleedingPower>(
            players.Where(static player => player.IsAlive),
            GoodbyeBleed * multiplier,
            Creature,
            null);
    }

    private async Task ExecuteAttack(
        int damage,
        int hits,
        IReadOnlyList<Creature> fixedTargets,
        bool formOne = false,
        bool playPerHitSfx = true)
    {
        _lastAttackAnimationTriggers.Clear();
        _lastAttackTargetCombatIds.Clear();
        string[] formThreeTriggers =
            ["AttackStrike", "AttackThrust", "AttackSlash"];
        string[] formThreeSfx =
            [
                StrongFleshSfxPath,
                StrongHorizontalSfxPath,
                StrongVerticalSfxPath
            ];
        for (int hit = 0;
             hit < hits && Creature.IsAlive;
             hit++)
        {
            IReadOnlyList<Creature> liveTargets =
                CombatTargets.DeterministicLiving(fixedTargets);
            if (liveTargets.Count == 0)
            {
                return;
            }

            string trigger = formOne
                ? "AttackThrust"
                : formThreeTriggers[hit % formThreeTriggers.Length];
            _lastAttackAnimationTriggers.Add(trigger);
            if (hit == 0)
            {
                _lastAttackTargetCombatIds.AddRange(
                    liveTargets.Select(static target =>
                        target.CombatId));
            }
            if (playPerHitSfx)
            {
                PlaySfx(
                    formOne
                        ? NormalFleshSfxPath
                        : formThreeSfx[
                            hit % formThreeSfx.Length]);
            }

            await CreatureCmd.TriggerAnim(
                Creature,
                trigger,
                AttackSegmentSeconds);
            await DamageAllPlayers(damage);
        }
    }

    private async Task DamageAllPlayers(int damage)
    {
        await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .Execute(null);
    }

    private async Task AddFearToTargets(
        IReadOnlyList<Creature> players)
    {
        foreach (Creature player in players.Where(
                     static player => player.IsAlive))
        {
            await CardPileCmdCompat
                .AddToCombatAndPreview<LanguageFloorFearCard>(
                    player,
                    PileType.Hand,
                    FearCards,
                    addedByPlayer: false);
        }
    }

    private async Task TransitionToSecond()
    {
        await TransitionPreamble();
        Form = LanguageFloorMimicryForm.Second;
        FormTwoActionsCompleted = 0;
        SkipCurrentFormTwoEnemyEnd = true;
        await ApplyFormStats(
            Form,
            FormTwoEntryHpPercent / 100m);
        await FinishTransition();
    }

    private async Task TransitionToThird(
        decimal secondFormRemainingRatio,
        bool skipCurrentEnemyEndRecovery)
    {
        await TransitionPreamble();
        Form = LanguageFloorMimicryForm.Third;
        MimicStacks = 0;
        RoundDamageTaken = 0;
        SkipCurrentFormThreeEnemyEndRecovery =
            skipCurrentEnemyEndRecovery;
        decimal minimum =
            FormThreeMinimumHpPercent / 100m;
        await ApplyFormStats(
            Form,
            Math.Max(minimum, secondFormRemainingRatio));
        await FinishTransition();
    }

    private async Task TransitionPreamble()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        await FakeDeathDebuffHelper.ClearDebuffs(Creature);
        if (Creature.Block > 0)
        {
            await CreatureCmd.LoseBlock(
                new ThrowingPlayerChoiceContext(),
                Creature,
                Creature.Block,
                Creature);
        }

        await RemoveFormPowers();
        await PresentationGuard.RunAsync(
            () => LanguageFloorMimicrySpecialEffects.PlayTransformationAsync(Creature),
            "LanguageFloorMimicry transformation effect");
    }

    private async Task FinishTransition()
    {
        EvolutionPending = false;
        PendingForm = -1;
        PreviousMove = -1;
        PlannedMoveEnhanced = false;
        PlannedTargetCombatId = 0;
        if (Form == LanguageFloorMimicryForm.Second)
        {
            await PowerCmdCompat.Apply<PlatingPower>(
                Creature,
                FormTwoInitialPlating,
                Creature,
                null);
            await PowerCmdCompat.Apply<BarricadePower>(
                Creature,
                1m,
                Creature,
                null,
                true);
        }

        await RefreshFormPowers();
        await PresentationGuard.RunAsync(ApplyVisualState, "LanguageFloorMimicry form visuals");
        ResetStateMachine();
        SetUpForCombat();
        if (Creature.CombatState is { } combatState)
        {
            Creature.PrepareForNextTurn(
                combatState.PlayerCreatures);
        }

        _isTransitioning = false;
    }

    private async Task ApplyFormStats(
        LanguageFloorMimicryForm form,
        decimal hpRatio)
    {
        int maxHp = await ResolveFormMaxHp(form);
        await CreatureCmd.SetMaxHp(Creature, maxHp);
        await CreatureCmd.SetCurrentHp(
            Creature,
            Math.Ceiling(maxHp * Math.Clamp(hpRatio, 0m, 1m)));
        if (Creature is LibraryCreature libraryCreature)
        {
            await MultiplayerScalingPatchHelper.SetMonsterBaseMaxAndCurrentChaoValue(
                libraryCreature,
                form switch
                {
                    LanguageFloorMimicryForm.First =>
                        FormOneMaxChao,
                    LanguageFloorMimicryForm.Second =>
                        FormTwoMaxChao,
                    _ => FormThreeMaxChao
                });
        }

        await ApplyFormResistances(form);
    }

    private async Task ApplyFormResistances(
        LanguageFloorMimicryForm form)
    {
        if (form == LanguageFloorMimicryForm.First
            || Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        LibraryCreatureResistanceData.Resistance physical =
            form == LanguageFloorMimicryForm.Second
                ? CreateFormTwoResistance()
                : CreateFormThreePhysicalResistance();
        LibraryCreatureResistanceData.Resistance chao =
            form == LanguageFloorMimicryForm.Second
                ? CreateFormTwoResistance()
                : CreateImmuneResistance();
        var context = new ThrowingPlayerChoiceContext();
        await LibraryCreatureCmd.SetPhysicalResistance(
            context,
            libraryCreature,
            Creature,
            LibraryDamageType.Slash,
            physical.Slash);
        await LibraryCreatureCmd.SetPhysicalResistance(
            context,
            libraryCreature,
            Creature,
            LibraryDamageType.Pierce,
            physical.Pierce);
        await LibraryCreatureCmd.SetPhysicalResistance(
            context,
            libraryCreature,
            Creature,
            LibraryDamageType.Blunt,
            physical.Blunt);
        await LibraryCreatureCmd.SetChaoResistance(
            context,
            libraryCreature,
            Creature,
            LibraryDamageType.Slash,
            chao.Slash);
        await LibraryCreatureCmd.SetChaoResistance(
            context,
            libraryCreature,
            Creature,
            LibraryDamageType.Pierce,
            chao.Pierce);
        await LibraryCreatureCmd.SetChaoResistance(
            context,
            libraryCreature,
            Creature,
            LibraryDamageType.Blunt,
            chao.Blunt);
    }

    private static LibraryCreatureResistanceData.Resistance
        CreateFormTwoResistance() =>
        new()
        {
            Slash = LibraryResistanceLevel.Resist,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure
        };

    private static LibraryCreatureResistanceData.Resistance
        CreateFormThreePhysicalResistance() =>
        new()
        {
            Slash = LibraryResistanceLevel.Resist,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Endure
        };

    private static LibraryCreatureResistanceData.Resistance
        CreateImmuneResistance() =>
        new(LibraryResistanceLevel.Immune);

    private async Task<int> ResolveFormMaxHp(
        LanguageFloorMimicryForm form)
    {
        int saved = form switch
        {
            LanguageFloorMimicryForm.First => FormOneMaxHp,
            LanguageFloorMimicryForm.Second => FormTwoMaxHp,
            _ => FormThreeMaxHp
        };
        if (saved > 0)
        {
            return saved;
        }

        int baseHp = form switch
        {
            LanguageFloorMimicryForm.First =>
                FormOneMaxHpBase,
            LanguageFloorMimicryForm.Second =>
                FormTwoMaxHpBase,
            _ => FormThreeMaxHpBase
        };
        decimal scaled = MultiplayerScalingPatchHelper.ScaleHpAmount(
            Creature.CombatState,
            this,
            baseHp);
        await CreatureCmd.SetMaxHp(Creature, scaled);
        int actual = Creature.MaxHp;
        switch (form)
        {
            case LanguageFloorMimicryForm.First:
                FormOneMaxHp = actual;
                break;
            case LanguageFloorMimicryForm.Second:
                FormTwoMaxHp = actual;
                break;
            default:
                FormThreeMaxHp = actual;
                break;
        }

        return actual;
    }

    private async Task RefreshFormPowers()
    {
        await RemoveFormPowers();
        if (Form == LanguageFloorMimicryForm.First)
        {
            await PowerCmdCompat.Ensure<
                LanguageFloorMimicryFormOneEvolutionPower>(Creature);
            return;
        }

        if (Form == LanguageFloorMimicryForm.Second)
        {
            await PowerCmdCompat.Ensure<
                LanguageFloorMimicryFormTwoEvolutionPower>(Creature);
            await PowerCmdCompat.Ensure<
                LanguageFloorMimicryFormTwoRegenerationPower>(Creature);
            await EnsureFormTwoThorns();
            return;
        }

        await PowerCmdCompat.Ensure<LanguageFloorMimicryHardenPower>(
            Creature);
        await PowerCmdCompat.Ensure<
            LanguageFloorMimicryFormThreeRegenerationPower>(Creature);
        await SyncMimicPower();
    }

    private async Task RemoveFormPowers()
    {
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorMimicryFormOneEvolutionPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorMimicryFormTwoEvolutionPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorMimicryFormTwoRegenerationPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorMimicryHardenPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorMimicryFormThreeRegenerationPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorMimicryMimicPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<ThornsPower>(Creature);
    }

    private Task EnsureFormTwoThorns() =>
        PowerCmdCompat.SetAmount<ThornsPower>(
            Creature,
            FormTwoThorns,
            Creature,
            null);

    private async Task GainMimic(int amount)
    {
        int next = Math.Clamp(
            MimicStacks + amount,
            0,
            MaximumMimicStacks);
        if (next == MimicStacks)
        {
            return;
        }

        MimicStacks = next;
        await SyncMimicPower();
    }

    private Task SyncMimicPower()
    {
        if (Form != LanguageFloorMimicryForm.Third
            || MimicStacks <= 0)
        {
            return PowerCmdCompat.RemoveIfPresent<
                LanguageFloorMimicryMimicPower>(Creature);
        }

        return PowerCmdCompat.SetAmount<
            LanguageFloorMimicryMimicPower>(
                Creature,
                MimicStacks,
                Creature,
                null);
    }

    private string ResolvePlannedMoveId(Rng rng)
    {
        if (Form == LanguageFloorMimicryForm.Second)
        {
            PreviousMove =
                (int)LanguageFloorMimicryMove.HardCocoon;
            PlannedMoveEnhanced = false;
            PlannedTargetCombatId = 0;
            return HardCocoonMoveId;
        }

        (LanguageFloorMimicryMove Move, int Weight)[] source =
            Form == LanguageFloorMimicryForm.First
                ? FormOneWeightedMoves
                : FormThreeWeightedMoves;
        var eligible = source
            .Where(entry => (int)entry.Move != PreviousMove)
            .ToArray();
        if (eligible.Length == 0)
        {
            eligible = source;
        }

        int roll = rng.NextInt(
            eligible.Sum(static entry => entry.Weight));
        LanguageFloorMimicryMove selected =
            eligible[^1].Move;
        foreach (var entry in eligible)
        {
            if (roll < entry.Weight)
            {
                selected = entry.Move;
                break;
            }

            roll -= entry.Weight;
        }

        PreviousMove = (int)selected;
        bool consumedMimic =
            LockPlannedMoveEnhancement(selected);
        PlannedTargetCombatId = 0;
        if (consumedMimic)
        {
            TaskHelper.RunSafely(SyncMimicPower());
        }

        return MoveId(selected);
    }

    private bool LockPlannedMoveEnhancement(
        LanguageFloorMimicryMove move)
    {
        PlannedMoveEnhanced = false;
        int cost = GetMimicCost(move);
        if (cost <= 0 || MimicStacks < cost)
        {
            return false;
        }

        MimicStacks -= cost;
        PlannedMoveEnhanced = true;
        return true;
    }

    private static int GetMimicCost(
        LanguageFloorMimicryMove move) =>
        move switch
        {
            LanguageFloorMimicryMove.Skin => SkinMimicCost,
            LanguageFloorMimicryMove.Imitate => ImitateMimicCost,
            LanguageFloorMimicryMove.Hello => HelloMimicCost,
            LanguageFloorMimicryMove.Goodbye => GoodbyeMimicCost,
            _ => 0
        };

    private static string MoveId(
        LanguageFloorMimicryMove move) =>
        move switch
        {
            LanguageFloorMimicryMove.ClumsyFlesh =>
                ClumsyFleshMoveId,
            LanguageFloorMimicryMove.EyeContact =>
                EyeContactMoveId,
            LanguageFloorMimicryMove.ExtendArm =>
                ExtendArmMoveId,
            LanguageFloorMimicryMove.HardCocoon =>
                HardCocoonMoveId,
            LanguageFloorMimicryMove.Mimic => MimicMoveId,
            LanguageFloorMimicryMove.Skin => SkinMoveId,
            LanguageFloorMimicryMove.Imitate =>
                ImitateMoveId,
            LanguageFloorMimicryMove.Hello => HelloMoveId,
            _ => GoodbyeMoveId
        };

    private int DeadlyDamage(int normal, int deadly) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            deadly,
            normal);

    private IReadOnlyList<Creature> LivingPlayers() =>
        ResolveLivingPlayers(Creature);

    private IReadOnlyList<Creature> PlannedAttackTargets() =>
        LivingPlayers();

    private static IReadOnlyList<Creature> ResolveLivingPlayers(
        Creature owner) =>
        CombatTargets.DeterministicLiving(
            owner.CombatState?.PlayerCreatures);

    private async Task ApplyVisualState()
    {
        if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals
            is LanguageFloorMimicryCreatureVisuals visuals)
        {
            visuals.SetForm(Form);
        }

        await Task.CompletedTask;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine =
            GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is not MoveState moveState)
            {
                continue;
            }

            foreach (AbstractIntent intent in moveState.Intents)
            {
                yield return intent;
            }
        }
    }

    internal string DebugChooseMove(Rng rng) =>
        ResolvePlannedMoveId(rng);

    internal void DebugSetMovePlanState(
        LanguageFloorMimicryForm form,
        int previousMove = -1,
        bool plannedMoveEnhanced = false,
        int plannedTargetCombatId = 0)
    {
        Form = form;
        PreviousMove = previousMove;
        PlannedMoveEnhanced = plannedMoveEnhanced;
        PlannedTargetCombatId = plannedTargetCombatId;
    }

    internal static IReadOnlyList<(
        LanguageFloorMimicryMove Move,
        int Weight)> DebugGetWeights(
            LanguageFloorMimicryForm form) =>
        form == LanguageFloorMimicryForm.First
            ? FormOneWeightedMoves
            : FormThreeWeightedMoves;

    internal static int DebugGetMoveDamage(
        LanguageFloorMimicryMove move,
        bool deadlyEnemies) =>
        move switch
        {
            LanguageFloorMimicryMove.ClumsyFlesh =>
                deadlyEnemies ? 8 : 7,
            LanguageFloorMimicryMove.EyeContact =>
                deadlyEnemies ? 19 : 17,
            LanguageFloorMimicryMove.ExtendArm =>
                deadlyEnemies ? 13 : 12,
            LanguageFloorMimicryMove.Mimic =>
                deadlyEnemies ? 9 : 8,
            LanguageFloorMimicryMove.Skin =>
                deadlyEnemies ? 8 : 7,
            LanguageFloorMimicryMove.Hello =>
                deadlyEnemies ? 33 : 30,
            LanguageFloorMimicryMove.Goodbye =>
                deadlyEnemies ? 25 : 20,
            _ => 0
        };

    internal IReadOnlyList<AbstractIntent> DebugGetIntents(
        LanguageFloorMimicryMove move)
    {
        MonsterMoveStateMachine stateMachine =
            GenerateMoveStateMachine();
        return ((MoveState)stateMachine.States[MoveId(move)])
            .Intents;
    }

    internal Task DebugPerformMove(
        LanguageFloorMimicryMove move) =>
        move switch
        {
            LanguageFloorMimicryMove.ClumsyFlesh =>
                ClumsyFleshMove([]),
            LanguageFloorMimicryMove.EyeContact =>
                EyeContactMove([]),
            LanguageFloorMimicryMove.ExtendArm =>
                ExtendArmMove([]),
            LanguageFloorMimicryMove.HardCocoon =>
                HardCocoonMove([]),
            LanguageFloorMimicryMove.Mimic =>
                MimicMove([]),
            LanguageFloorMimicryMove.Skin =>
                SkinMove([]),
            LanguageFloorMimicryMove.Imitate =>
                ImitateMove([]),
            LanguageFloorMimicryMove.Hello =>
                HelloMove([]),
            _ => GoodbyeMove([])
        };

    internal Task DebugSetMimicStacks(int amount)
    {
        MimicStacks = Math.Clamp(
            amount,
            0,
            MaximumMimicStacks);
        return SyncMimicPower();
    }

    internal async Task DebugPlanMove(
        LanguageFloorMimicryMove move)
    {
        PreviousMove = (int)move;
        LockPlannedMoveEnhancement(move);
        PlannedTargetCombatId = 0;
        await SyncMimicPower();
    }

    internal Task DebugGainMimic(int amount) =>
        GainMimic(amount);

    internal Task DebugTransitionToSecond() =>
        TransitionToSecond();

    internal Task DebugTransitionToThird(decimal ratio) =>
        TransitionToThird(
            ratio,
            skipCurrentEnemyEndRecovery: false);

    internal void DebugSetRoundDamageTaken(int amount)
    {
        RoundDamageTaken = Math.Max(0, amount);
    }

    private static void PlaySfx(string path)
    {
        LocalOggOneShotPlayer.Play(path, -2f);
    }

}
