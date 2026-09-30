using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Art;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ArtFloorNostalgicScentBoss : LiberationPhaseBossMonster
{
    private const int Phase = 5;
    internal const string WinterBeginningMoveId = "WINTER_BEGINNING";
    private const string BlossomsOnBodyMoveId = "BLOSSOMS_ON_BODY";
    private const string FadingAutumnMoveId = "FADING_AUTUMN";
    private const string SpringOriginMoveId = "SPRING_ORIGIN";
    internal const string NostalgicScentEgoMoveId = "NOSTALGIC_SCENT_EGO";
    internal const float AttackSegmentDelaySeconds = 0.85f;
    private const float SegmentDelaySeconds = AttackSegmentDelaySeconds;

    public const int AutumnBlock = 69;
    public const int AutumnThorns = 9;
    public const int SpringStrength = 4;
    public const int FragrancePerHit = 2;
    public const int AtonementCrownAmount = 1;

    public const string Root = "res://images/monsters/art_floor/nostalgic_scent/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string RangedTexturePath = Root + "ranged.png";
    public const string BluntTexturePath = Root + "blunt.png";
    public const string PierceTexturePath = Root + "pierce.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string GuardTexturePath = Root + "guard.png";
    public const string EgoS1TexturePath = Root + "ego_s1.png";
    public const string EgoS2TexturePath = Root + "ego_s2.png";
    public const string EgoS3TexturePath = Root + "ego_s3.png";

    public const string SfxRoot = "res://audio/sfx/art_floor/nostalgic_scent/";
    public const string AttackSfxPath = SfxRoot + "attack.ogg";
    public const string RangedSfxPath = SfxRoot + "ranged.ogg";
    public const string EgoSfxPath = SfxRoot + "ego.ogg";
    public const string EgoFinishSfxPath = SfxRoot + "ego_finish.ogg";
    public const string GuardSfxPath = SfxRoot + "guard.ogg";

    private int _baseCadenceIndex;
    private bool _egoQueued;
    private MoveState? _winterState;
    private MoveState? _blossomsState;
    private MoveState? _autumnState;
    private MoveState? _springState;
    private MoveState? _egoState;

    public override int LiberationPhase => Phase;

    internal bool IsWinterBeginningQueued => NextMove.Id == WinterBeginningMoveId;

    public override bool ShouldDisappearFromDoom => false;

    public override int DefaultChaoResistance => 150;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 376, 273);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 380, 275);

    private int WinterDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 24, 19);

    private int BlossomsDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 15, 12);

    private int SpringDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 34, 30);

    private int EgoDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, ArtFloorEgoNumbers.NostalgicScentUpgradedDamage, ArtFloorEgoNumbers.NostalgicScentDamage);

    public override IEnumerable<string> AssetPaths =>
        ArtFloorNostalgicScentCreatureVisuals.Profile.AssetPaths
            .Concat([
                AttackSfxPath,
                RangedSfxPath,
                EgoSfxPath,
                EgoFinishSfxPath,
                GuardSfxPath,
                ImageHelper.GetImagePath(NostalgicScentEgoCard.GetPortraitResourcePath())
            ])
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _baseCadenceIndex = 0;
        _egoQueued = false;

        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<NostalgicScentEgoCard>());

        if (CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        ArtFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<ArtFloorErosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorSuffocatingAtonementPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorUnfadingFlowerPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorClayDollPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature
            || Creature.CombatState?.Encounter is not ArtFloorLiberationEncounter encounter)
        {
            return Task.CompletedTask;
        }

        return encounter.OnPhaseBossDeath(this, wasRemovalPrevented, deathAnimLength);
    }

    public Task QueuePetalEgo()
    {
        if (_egoQueued || Creature.IsDead || _egoState == null)
        {
            return Task.CompletedTask;
        }

        _egoQueued = true;
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        _winterState = new MoveState(
            WinterBeginningMoveId,
            WinterBeginningMove,
            new CombinedTargetedAttackDebuffIntent(
                () => WinterDamage,
                null,
                descriptionKey: null,
                playerTargetDescriptionKey: null,
                useVanillaPlayerTargetIntentVisual: true,
                IntentBadge.FromPower<ArtFloorAtonementCrownPower>(AtonementCrownAmount)));

        _blossomsState = new MoveState(
            BlossomsOnBodyMoveId,
            BlossomsOnBodyMove,
            new CombinedAttackDebuffIntent(
                () => BlossomsDamage,
                () => 3,
                null,
                IntentBadge.FromPower<ArtFloorFragrancePower>(FragrancePerHit)),
            new DebuffIntent());

        _autumnState = new MoveState(
            FadingAutumnMoveId,
            FadingAutumnMove,
            new CombinedDefendBuffIntent(
                AutumnBlock,
                null,
                IntentBadge.FromPower<ThornsPower>(AutumnThorns)));

        _springState = new MoveState(
            SpringOriginMoveId,
            SpringOriginMove,
            new CombinedAttackBuffIntent(
                () => SpringDamage,
                null,
                null,
                IntentBadge.Strength(SpringStrength)));

        _egoState = new MoveState(
            NostalgicScentEgoMoveId,
            NostalgicScentEgoMove,
            CreateNostalgicScentEgoIntent());

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(_egoState, () => _egoQueued && AnyLivingPlayerHasCrown());
        chooser.AddState(_winterState, () => !AnyLivingPlayerHasCrown());
        chooser.AddState(_winterState, () => _baseCadenceIndex == 0 && AnyLivingPlayerHasCrown());
        chooser.AddState(_blossomsState, () => _baseCadenceIndex == 1 && AnyLivingPlayerHasCrown());
        chooser.AddState(_autumnState, () => _baseCadenceIndex == 2 && AnyLivingPlayerHasCrown());
        chooser.AddState(_springState, () => true);

        _winterState.FollowUpState = chooser;
        _blossomsState.FollowUpState = chooser;
        _autumnState.FollowUpState = chooser;
        _springState.FollowUpState = chooser;
        _egoState.FollowUpState = chooser;
        reviveAndEmpower.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[]
            {
                reviveAndEmpower,
                _winterState,
                _blossomsState,
                _autumnState,
                _springState,
                _egoState,
                chooser
            },
            chooser);
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter is ArtFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private async Task WinterBeginningMove(IReadOnlyList<Creature> targets)
    {
        await ApplyDustbornWinterStasis();

        Creature? target = ResolveWinterTarget(targets);
        if (target != null)
        {
            IReadOnlyList<DamageResult> results = await ExecuteWinterBeginningAttack(target);
            if (!CombatManager.Instance.IsOverOrEnding
                && !Creature.IsDead
                && target.IsAlive
                && results.Any(result => result.Receiver == target))
            {
                await PowerCmdCompat.Apply<ArtFloorAtonementCrownPower>(
                    target,
                    AtonementCrownAmount,
                    Creature,
                    null);
            }
        }

        AdvanceBaseCadence();
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteWinterBeginningAttack(Creature target)
    {
        if (CombatManager.Instance.IsOverOrEnding || Creature.IsDead || !target.IsAlive)
        {
            return Array.Empty<DamageResult>();
        }

        await CreatureCmd.TriggerAnim(Creature, "Ranged", SegmentDelaySeconds);
        LocalOggOneShotPlayer.Play(RangedSfxPath, -2f);
        VfxCmd.PlayOnCreatureCenter(target, "vfx/vfx_attack_blunt");

        IEnumerable<DamageResult> results = await CreatureCmdCompat.Damage(
            new ThrowingPlayerChoiceContext(),
            target,
            WinterDamage,
            ValueProp.Move,
            Creature);

        return results.ToArray();
    }

    private async Task BlossomsOnBodyMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < 3; i++)
        {
            string anim = i % 2 == 0 ? "Pierce" : "Blunt";
            AttackCommand attack = await ExecuteGroupAttack(BlossomsDamage, anim, "vfx/vfx_attack_slash", AttackSfxPath);
            foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
            {
                await PowerCmdCompat.ApplyDebuff<ArtFloorFragrancePower>(
                    target,
                    FragrancePerHit,
                    Creature,
                    null);
            }
        }

        AdvanceBaseCadence();
    }

    private async Task FadingAutumnMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(GuardSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.2f);
        await CreatureCmd.GainBlock(Creature, AutumnBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<ThornsPower>(Creature, AutumnThorns, Creature, null);
        AdvanceBaseCadence();
    }

    private async Task SpringOriginMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteGroupAttack(SpringDamage, "Blunt", "vfx/vfx_attack_blunt", AttackSfxPath);

        IReadOnlyList<Creature> allies = CombatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .ToArray();
        if (allies.Count > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(allies, SpringStrength, Creature, null);
        }

        AdvanceBaseCadence();
    }

    private async Task NostalgicScentEgoMove(IReadOnlyList<Creature> targets)
    {
        _egoQueued = false;

        for (int i = 0; i < ArtFloorEgoNumbers.NostalgicScentHitCount; i++)
        {
            string anim = i switch
            {
                0 or 3 or 6 => "EgoS1",
                1 or 4 => "EgoS2",
                _ => "EgoS3"
            };
            string sfx = i == ArtFloorEgoNumbers.NostalgicScentHitCount - 1 ? EgoFinishSfxPath : EgoSfxPath;
            AttackCommand attack = await ExecuteGroupAttack(EgoDamage, anim, "vfx/vfx_attack_slash", sfx);
            foreach (Creature target in AttackCommandCompat.Results(attack)
                .Where(static result => result.Receiver.IsPlayer && result.WasFullyBlocked)
                .Select(static result => result.Receiver)
                .Distinct())
            {
                await CreatureCmd.LoseMaxHp(
                    new ThrowingPlayerChoiceContext(),
                    target,
                    ArtFloorEgoNumbers.NostalgicScentMaxHpLossOnFullBlock,
                    isFromCard: true);
            }
        }
    }

    private Task<AttackCommand> ExecuteGroupAttack(
        int damage,
        string anim,
        string hitFx,
        string sfxPath)
    {
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(anim, SegmentDelaySeconds)
            .AfterAttackerAnim(() =>
            {
                LocalOggOneShotPlayer.Play(sfxPath, -2f);
                return Task.CompletedTask;
            })
            .WithHitFx(hitFx)
            .Execute(null);
    }

    private async Task ApplyDustbornWinterStasis()
    {
        if (CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
        {
            await encounter.ApplyNostalgicScentWinterStasis(CombatState);
            return;
        }

        if (CombatState == null)
        {
            return;
        }

        foreach (Creature dustborn in CombatState.Enemies.Where(static enemy => enemy.Monster is ArtFloorDustbornPerson && enemy.IsAlive))
        {
            await PowerCmdCompat.Apply<ArtFloorDustbornWinterStasisPower>(dustborn, 1m, Creature, null, silent: true);
            if (dustborn.Monster is ArtFloorDustbornPerson person)
            {
                await person.QueueWinterStasis();
            }
        }
    }

    private Creature? ResolveWinterTarget(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> livingTargets = (targets ?? Array.Empty<Creature>())
            .Where(static target => target is { IsAlive: true, IsPlayer: true })
            .ToArray();
        if (livingTargets.Count > 0)
        {
            return RunRng.MonsterAi.NextItem(livingTargets);
        }

        IReadOnlyList<Creature> fallbackTargets = CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray() ?? Array.Empty<Creature>();
        return fallbackTargets.Count > 0
            ? RunRng.MonsterAi.NextItem(fallbackTargets)
            : null;
    }

    private bool AnyLivingPlayerHasCrown()
    {
        return CombatState?.PlayerCreatures
            .Any(static player => player.IsAlive && player.HasPower<ArtFloorAtonementCrownPower>()) == true;
    }

    private void AdvanceBaseCadence()
    {
        _baseCadenceIndex = (_baseCadenceIndex + 1) % 4;
    }

    private PlayCardAttackIntent<NostalgicScentEgoCard> CreateNostalgicScentEgoIntent()
    {
        return new PlayCardAttackIntent<NostalgicScentEgoCard>(
            "NOSTALGIC_SCENT_EGO_CARD",
            () => EgoDamage,
            static (card, _) => card.UpgradePreview(),
            () => ArtFloorEgoNumbers.NostalgicScentHitCount,
            badges: [IntentBadge.FromPower<ArtFloorAtonementCrownPower>(ArtFloorEgoNumbers.NostalgicScentMaxHpLossOnFullBlock)]);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new CombinedTargetedAttackDebuffIntent(
            () => WinterDamage,
            null,
            descriptionKey: null,
            playerTargetDescriptionKey: null,
            useVanillaPlayerTargetIntentVisual: true,
            IntentBadge.FromPower<ArtFloorAtonementCrownPower>(AtonementCrownAmount));
        yield return new CombinedAttackDebuffIntent(
            () => BlossomsDamage,
            () => 3,
            null,
            IntentBadge.FromPower<ArtFloorFragrancePower>(FragrancePerHit));
        yield return new DebuffIntent();
        yield return new CombinedDefendBuffIntent(
            AutumnBlock,
            null,
            IntentBadge.FromPower<ThornsPower>(AutumnThorns));
        yield return new CombinedAttackBuffIntent(
            () => SpringDamage,
            null,
            null,
            IntentBadge.Strength(SpringStrength));
        yield return new DetailedBuffIntent<StrengthPower>(SpringStrength);
        yield return CreateNostalgicScentEgoIntent();
    }

    private static IReadOnlyList<Creature> GetUnblockedPlayerHitTargets(AttackCommand attack)
    {
        return AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsAlive && result.Receiver.IsPlayer && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
    }
}
