using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.History;

public sealed class HistoryFloorForgottenBoss : LiberationPhaseBossMonster
{
    private const int Phase = 2;

    public override int DefaultChaoResistance => 40;

    private const int NormalMinHp = 135;
    private const int NormalMaxHp = 139;
    private const int HighAscensionMinHp = 140;
    private const int HighAscensionMaxHp = 143;
    private const int BaseAttackDamage = 19;
    private const int HighAscensionAttackDamage = 21;
    private const int CautiousLoveBlock = 8;
    private const int ExpressAffectionBlock = 4;
    private const int RoughLoveHits = 3;
    private const int ExpressAffectionHits = 2;
    private const int FlawTurns = 2;
    private const int FlawAmount = 1;
    


    private const string CautiousLoveMoveId = "CAUTIOUS_LOVE";
    private const string ExpressAffectionMoveId = "EXPRESS_AFFECTION";
    private const string RoughLoveMoveId = "ROUGH_LOVE";
    private const string LongingEmbraceMoveId = "LONGING_EMBRACE";
    internal const string BackgroundTextScope = "history_floor_liberation_phase_2";
    private const float BackgroundTextIntervalSeconds = 5f;

    public const string IdleTexturePath = HistoryFloorAssets.ForgottenIdleTexture;
    public const string StrikeTexturePath = HistoryFloorAssets.ForgottenAttackStrikeTexture;
    public const string SlashTexturePath = HistoryFloorAssets.ForgottenAttackSlashTexture;
    public const string SpecialTexturePath = HistoryFloorAssets.ForgottenSpecialTexture;
    public const string HitTexturePath = HistoryFloorAssets.ForgottenHitTexture;
    public const string AttackSfxPath = HistoryFloorAssets.HappyTeddyForgottenAttackSfx;
    public const string ParrySfxPath = HistoryFloorAssets.HappyTeddyForgottenParrySfx;
    public const string LongingEmbraceSfxPath = HistoryFloorAssets.HappyTeddyForgottenEmbraceSfx;

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 190f, 980f, 470f);
    private static readonly string[] BackgroundTextLineKeys =
    [
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.0",
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.1",
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.2",
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.3",
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.4",
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.5",
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.6",
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.7",
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.8",
        "HISTORY_FLOOR_FORGOTTEN_BOSS.backgroundText.normal.9"
    ];

    private static readonly string[] SfxAssetPaths =
    [
        AttackSfxPath,
        ParrySfxPath,
        LongingEmbraceSfxPath
    ];

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Blunt = LibraryResistanceLevel.Resist,
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Blunt = LibraryResistanceLevel.Normal,
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist
    };

    private int _baseCadenceIndex;
    private bool _longingEmbraceQueued;
    private bool _backgroundMoonTextLoopStarted;
    private MoveState? _longingEmbraceState;

    public override int LiberationPhase => Phase;

    /// <summary>死亡动画时长；转阶段的假死返回 0，见 <see cref="LayeredBossSpine.DeathLength"/>。</summary>
    public override float DeathAnimLengthOverride => LayeredBossSpine.DeathLength(this);

    protected override bool ReviveTriggerPlaysHitAnimation => true;

    protected override string? ReviveAndEmpowerAnimation => "Cast";

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMinHp, NormalMinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMaxHp, NormalMaxHp);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionAttackDamage, BaseAttackDamage);

    private int ExpressionAffectionDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 7, 6);

    private int RoughLoveDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 5, 4);

    private int LongingEmbraceDamage => ResolveLongingEmbraceDamage();

    public static int ResolveLongingEmbraceDamage() =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HistoryFloorEgoNumbers.LongingEmbraceHighAscensionDamage, HistoryFloorEgoNumbers.LongingEmbraceBaseDamage);

    public override IEnumerable<string> AssetPaths =>
        HistoryFloorForgottenCreatureVisuals.Profile.AssetPaths
            .Concat(SfxAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _baseCadenceIndex = 0;
        _longingEmbraceQueued = false;
        _backgroundMoonTextLoopStarted = false;

        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<ForgottenLongingEmbraceEgoCard>());

        if (CombatState?.Encounter is HistoryFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        HistoryFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<HistoryFloorCorrosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ForgottenAffectionAttackPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ForgottenLongingEmbracePower>(Creature, 1m, Creature, null, silent: true);
        StartBackgroundMoonTextLoop();
    }

    public override Task BeforeCombatStart()
    {
        HistoryFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        StartBackgroundMoonTextLoop();

        return Task.CompletedTask;
    }

    private void StartBackgroundMoonTextLoop() =>
        MonsterMoonTextLoop.StartOnce(
            this,
            ref _backgroundMoonTextLoopStarted,
            BackgroundTextScope,
            BackgroundTextLineKeys,
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);

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

    public async Task QueueLongingEmbraceFromAffection()
    {
        if (_longingEmbraceQueued || Creature.IsDead || _longingEmbraceState == null)
        {
            return;
        }

        _longingEmbraceQueued = true;
        SetMoveImmediate(_longingEmbraceState, forceTransition: true);

        NCreature? creatureNode = CombatQueries.CreatureNodeOf(this);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        var cautiousLove = new MoveState(
            CautiousLoveMoveId,
            CautiousLoveMove,
            new SingleAttackIntent(AttackDamage),
            new DefendIntent(),
            new DebuffIntent());

        var expressAffection = new MoveState(
            ExpressAffectionMoveId,
            ExpressAffectionMove,
            new MultiAttackIntent(ExpressionAffectionDamage, ExpressAffectionHits),
            new DefendIntent(),
            new DebuffIntent());

        var roughLove = new MoveState(
            RoughLoveMoveId,
            RoughLoveMove,
            new MultiAttackIntent(RoughLoveDamage, RoughLoveHits));

        var longingEmbrace = new MoveState(
            LongingEmbraceMoveId,
            LongingEmbraceMove,
            new PlayCardAttackIntent<ForgottenLongingEmbraceEgoCard>(
                "FORGOTTEN_LONGING_EMBRACE_EGO_CARD",
                () => LongingEmbraceDamage,
                static (card, damages) => { card.UpgradePreview(); card.SetPreviewDamage(damages[0]); }));

        _longingEmbraceState = longingEmbrace;

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(longingEmbrace, () => _longingEmbraceQueued);
        chooser.AddState(cautiousLove, () => _baseCadenceIndex == 0);
        chooser.AddState(expressAffection, () => _baseCadenceIndex == 1);
        chooser.AddState(roughLove, () => true);

        cautiousLove.FollowUpState = chooser;
        expressAffection.FollowUpState = chooser;
        roughLove.FollowUpState = chooser;
        longingEmbrace.FollowUpState = chooser;
        reviveAndEmpower.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[] { reviveAndEmpower, cautiousLove, expressAffection, roughLove, longingEmbrace, chooser },
            chooser);
    }

    private async Task CautiousLoveMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> results = await ExecuteAttackSegment("AttackStrike", AttackDamage, AttackSfxPath);
        await ApplyAffectionFromResults(results);
        await ApplyNextTurnStrengthFromMaxAffection();
        await CreatureCmd.GainBlock(Creature, CautiousLoveBlock, ValueProp.Move, null);
        foreach (Creature target in targets.Where(static target => target.IsAlive))
        {
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(target, FlawAmount, FlawTurns, Creature, null);
        }

        AdvanceBaseCadence();
    }

    private async Task ExpressAffectionMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < ExpressAffectionHits; i++)
        {
            if (Creature.IsDead) return;
            string animation = i % 2 == 0 ? "AttackStrike" : "AttackSlash";
            IReadOnlyList<DamageResult> results = await ExecuteAttackSegment(animation, ExpressionAffectionDamage, AttackSfxPath);
            IReadOnlyList<Creature> unblockedTargets = results
                .Where(static result => result.Receiver.IsPlayer && result.Receiver.IsAlive && result.UnblockedDamage > 0)
                .Select(static result => result.Receiver)
                .Distinct()
                .ToArray();

            await ApplyAffectionFromResults(results);
            foreach (Creature target in unblockedTargets)
            {
                await LibraryPowerCmd.Apply<LibraryVulnerablePower>(target, 1, turns:1, Creature, null);
            }
        }

        await ApplyNextTurnStrengthFromMaxAffection();
        await CreatureCmd.GainBlock(Creature, ExpressAffectionBlock, ValueProp.Move, null);
        AdvanceBaseCadence();
    }

    private async Task RoughLoveMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < RoughLoveHits; i++)
        {
            if (Creature.IsDead) return;
            string animation = i % 2 == 0 ? "AttackSlash" : "AttackStrike";
            IReadOnlyList<DamageResult> results = await ExecuteAttackSegment(animation, RoughLoveDamage, AttackSfxPath);
            await ApplyAffectionFromResults(results);
        }

        await ApplyNextTurnStrengthFromMaxAffection();
        AdvanceBaseCadence();
    }

    private async Task LongingEmbraceMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(LongingEmbraceSfxPath, -2f);

        AttackCommand attack = await DamageCmd.Attack(LongingEmbraceDamage)
            .FromMonster(this)
            .WithAttackerAnim("LongingEmbrace", 2.0f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await ApplyAffectionFromResults(AttackCommandCompat.Results(attack));
        await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
            targets.Where(static target => target.IsAlive),
            HistoryFloorEgoNumbers.LongingEmbraceConfusion,
            Creature,
            null);

        _longingEmbraceQueued = false;
        if (CombatState != null)
        {
            await ForgottenAffectionPower.ResetAffectionStacks(CombatState.PlayerCreatures);
        }
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter is HistoryFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private async Task<IReadOnlyList<DamageResult>> ExecuteAttackSegment(string animation, int damage, string sfxPath)
    {
        if (Creature == null || Creature.CombatState == null)
        {
            return Array.Empty<DamageResult>();
        }

        LocalOggOneShotPlayer.Play(sfxPath, -2f);

        AttackCommand attack = await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(animation, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        return AttackCommandCompat.Results(attack);
    }

    private async Task ApplyAffectionFromResults(IEnumerable<DamageResult> results)
    {
        foreach (DamageResult result in results.Where(static result => result.Receiver.IsPlayer && result.UnblockedDamage > 0))
        {
            if (!result.Receiver.IsAlive)
            {
                continue;
            }

            await PowerCmdCompat.Apply<ForgottenAffectionPower>(
                result.Receiver,
                ForgottenAffectionAttackPower.AffectionPerHit,
                Creature,
                null);
        }
    }

    private async Task ApplyNextTurnStrengthFromMaxAffection()
    {
        if (!Creature.IsAlive || CombatState == null)
        {
            return;
        }

        int maxAffection = 0;
        foreach (Creature player in CombatState.PlayerCreatures)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            int affectionAmount = player.GetPower<ForgottenAffectionPower>()?.Amount ?? 0;
            if (affectionAmount > maxAffection)
            {
                maxAffection = affectionAmount;
            }
        }

        if (maxAffection > 0)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, maxAffection, Creature, null);
        }
    }

    private void AdvanceBaseCadence()
    {
        _baseCadenceIndex = (_baseCadenceIndex + 1) % 3;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new SingleAttackIntent(AttackDamage);
        yield return new MultiAttackIntent(AttackDamage, ExpressAffectionHits);
        yield return new MultiAttackIntent(AttackDamage, RoughLoveHits);
        yield return new DefendIntent();
        yield return new DebuffIntent();
        yield return new HealIntent();
        yield return new BuffIntent();
        yield return new PlayCardAttackIntent<ForgottenLongingEmbraceEgoCard>(
            "FORGOTTEN_LONGING_EMBRACE_EGO_CARD",
            () => LongingEmbraceDamage,
            static (card, damages) => { card.UpgradePreview(); card.SetPreviewDamage(damages[0]); });
    }

    internal void StopBackgroundMoonTextLoop() =>
        MonsterMoonTextLoop.Stop(this, BackgroundTextScope);
}
