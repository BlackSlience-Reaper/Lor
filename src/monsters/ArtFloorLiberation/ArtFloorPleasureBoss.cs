using LibraryLib.Models;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.cards.ArtFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.ArtFloorLiberation;
using LibraryOfRuina.visuals.ArtFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.ArtFloorLiberation;

public sealed class ArtFloorPleasureBoss : LibraryMonsterModel, ILiberationPrimaryPhaseBoss
{
    private const int Phase = 4;
    private const string GrinningMoveId = "GRINNING";
    private const string TrustGameMoveId = "TRUST_GAME";
    private const string PiercingPleasureMoveId = "PIERCING_PLEASURE";
    private const string PleasureEgoMoveId = "PLEASURE_EGO";
    private const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";
    private const float SegmentDelaySeconds = AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds;

    private const int GrinningBlock = 16;
    private const int GrinningPleasureCards = 2;
    private const int TrustGamePleasureCards = 3;
    private const int TrustGameBlock = 22;
    private const int PiercingPleasureHits = 6;
    private const int PiercingPleasureCards = 5;

    public const string Root = "res://images/monsters/art_floor/pleasure/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string AttackBluntTexturePath = Root + "attack_blunt.png";
    public const string AttackPierceTexturePath = Root + "attack_pierce.png";
    public const string AttackSlashTexturePath = Root + "attack_slash.png";
    public const string EgoS1TexturePath = Root + "ego_s1.png";
    public const string EgoS2TexturePath = Root + "ego_s2.png";

    private int _baseCadenceIndex;
    private bool _egoQueued;
    private MoveState? _egoState;
    private MoveState? _reviveAndEmpowerState;

    public int LiberationPhase => Phase;

    public override bool ShouldDisappearFromDoom => false;

    public override int DefaultChaoResistance => 180;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 396, 310);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 400, 324);

    private int TrustGameDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 29, 23);

    private int PiercingPleasureDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 4);

    private int PleasureEgoDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, PleasureEgoCard.UpgradedDamage, PleasureEgoCard.Damage);

    private int PleasureEgoFinalDamage => PleasureEgoCard.FinalDamage;

    public override IEnumerable<string> AssetPaths =>
        ArtFloorPleasureCreatureVisuals.Profile.AssetPaths
            .Concat(new[]
            {
                SpinyBus.SpinyBus.AttackSfxPath,
                SpinyBus.SpinyBus.ParrySfxPath,
                SpinyBus.SpinyBus.HitSfxPath,
                ImageHelper.GetImagePath(PleasureCard.GetPortraitResourcePath()),
                ImageHelper.GetImagePath(PleasureEgoCard.GetPortraitResourcePath())
            })
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _baseCadenceIndex = 0;
        _egoQueued = false;

        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<PleasureCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<PleasureEgoCard>());

        if (CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<ArtFloorErosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorPleasureJoyThornsPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorPleasureSoftBodyPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorPleasureUnbearablePleasurePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorPleasureExplodingHeadPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
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
            || Creature.CombatState?.Encounter is not ArtFloorLiberationEncounter encounter)
        {
            return Task.CompletedTask;
        }

        return encounter.OnPhaseBossDeath(this, wasRemovalPrevented, deathAnimLength);
    }

    public Task QueuePleasureEgo()
    {
        if (_egoQueued || Creature.IsDead || _egoState == null)
        {
            return Task.CompletedTask;
        }

        _egoQueued = true;
        return Task.CompletedTask;
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
            SetMoveImmediate(_reviveAndEmpowerState, forceTransition: true);
        }
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

        var grinning = new MoveState(
            GrinningMoveId,
            GrinningMove,
            CreateBlockIntent(),
            new DetailedStatusCardIntent<PleasureCard>(
                GrinningPleasureCards,
                PileType.Draw,
                showSingleTargetMarker: false));

        var trustGame = new MoveState(
            TrustGameMoveId,
            TrustGameMove,
            new SingleAttackIntent(() => TrustGameDamage),
            new DetailedStatusCardIntent<PleasureCard>(
                TrustGamePleasureCards,
                PileType.Draw,
                showSingleTargetMarker: false),
            CreateBlockIntent());

        var piercingPleasure = new MoveState(
            PiercingPleasureMoveId,
            PiercingPleasureMove,
            new MultiAttackIntent(PiercingPleasureDamage, PiercingPleasureHits),
            new DetailedStatusCardIntent<PleasureCard>(
                PiercingPleasureCards,
                PileType.Draw,
                showSingleTargetMarker: false));

        _egoState = new MoveState(
            PleasureEgoMoveId,
            PleasureEgoMove,
            CreatePleasureEgoIntent(),
            new CombinedAttackBuffIntent(
                () => PleasureEgoFinalDamage));

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(_egoState, () => _egoQueued);
        chooser.AddState(grinning, () => _baseCadenceIndex == 0);
        chooser.AddState(trustGame, () => _baseCadenceIndex == 1);
        chooser.AddState(piercingPleasure, () => true);

        grinning.FollowUpState = chooser;
        trustGame.FollowUpState = chooser;
        piercingPleasure.FollowUpState = chooser;
        _egoState.FollowUpState = chooser;
        _reviveAndEmpowerState.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[]
            {
                _reviveAndEmpowerState,
                grinning,
                trustGame,
                piercingPleasure,
                _egoState,
                chooser
            },
            chooser);
    }

    private async Task ReviveAndEmpowerMove(IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        await Cmd.CustomScaledWait(0.3f, 0.6f);

        if (Creature.CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseTransition(this);
        }
    }

    private async Task GrinningMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(Creature, GrinningBlock, ValueProp.Move, null);
        await AddPleasureCardsToPlayers(GrinningPleasureCards);
        AdvanceBaseCadence();
    }

    private async Task TrustGameMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteGroupAttack(TrustGameDamage, "Pierce", "vfx/vfx_dramatic_stab");
        await AddPleasureCardsToPlayers(TrustGamePleasureCards);

        await CreatureCmd.GainBlock(Creature, TrustGameBlock, ValueProp.Move, null);
        AdvanceBaseCadence();
    }

    private async Task PiercingPleasureMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < PiercingPleasureHits; i++)
        {
            string anim = (i % 3) switch
            {
                0 => "Slash",
                1 => "Pierce",
                _ => "Blunt"
            };
            string hitFx = anim switch
            {
                "Pierce" => "vfx/vfx_dramatic_stab",
                "Blunt" => "vfx/vfx_attack_blunt",
                _ => "vfx/vfx_attack_slash"
            };

            await ExecuteGroupAttack(PiercingPleasureDamage, anim, hitFx);
        }

        await AddPleasureCardsToPlayers(PiercingPleasureCards);
        AdvanceBaseCadence();
    }

    private async Task PleasureEgoMove(IReadOnlyList<Creature> targets)
    {
        _egoQueued = false;

        var bleedTargets = new HashSet<Creature>();
        for (int i = 0; i < PleasureEgoCard.HitCount; i++)
        {
            string anim = i % 2 == 0 ? "EgoS1" : "EgoS2";
            AttackCommand attack = await ExecuteGroupAttack(PleasureEgoDamage, anim, "vfx/vfx_attack_slash");
            foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
            {
                bleedTargets.Add(target);
            }
        }

        if (bleedTargets.Count > 0)
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                bleedTargets,
                PleasureEgoCard.BleedAmount,
                Creature,
                null);
        }

        await ExecuteGroupAttack(PleasureEgoFinalDamage, "EgoS2", "vfx/vfx_attack_blunt", SpinyBus.SpinyBus.ParrySfxPath);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, PleasureEgoCard.Strength, Creature, null);
    }

    private Task<AttackCommand> ExecuteGroupAttack(
        int damage,
        string anim,
        string hitFx,
        string sfxPath = SpinyBus.SpinyBus.AttackSfxPath)
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

    private Task AddPleasureCardsToPlayers(int count)
    {
        IReadOnlyList<Creature> players = CombatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();

        return CardPileCmdCompat.AddToCombatAndPreview<PleasureCard>(
            players,
            PileType.Draw,
            count,
            addedByPlayer: false);
    }

    private void AdvanceBaseCadence()
    {
        _baseCadenceIndex = (_baseCadenceIndex + 1) % 3;
    }

    private Task RefreshNodeIntents()
    {
        return NCombatRoom.Instance?.GetCreatureNode(Creature)?.RefreshIntents() ?? Task.CompletedTask;
    }

    private static IReadOnlyList<Creature> GetUnblockedPlayerHitTargets(AttackCommand attack)
    {
        return AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsAlive && result.Receiver.IsPlayer && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
    }

    private PlayCardAttackIntent<PleasureEgoCard> CreatePleasureEgoIntent()
    {
        return new PlayCardAttackIntent<PleasureEgoCard>(
            "PLEASURE_EGO_CARD",
            () => PleasureEgoDamage,
            null,
            () => PleasureEgoCard.HitCount,
            new[]
            {
                IntentBadge.Bleed(PleasureEgoCard.BleedAmount)
            },
            () => PleasureEgoFinalDamage);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return CreateBlockIntent();
        yield return new DetailedStatusCardIntent<PleasureCard>(
            GrinningPleasureCards, PileType.Draw, showSingleTargetMarker: false);
        yield return new SingleAttackIntent(() => TrustGameDamage);
        yield return new DetailedStatusCardIntent<PleasureCard>(
            TrustGamePleasureCards, PileType.Draw, showSingleTargetMarker: false);
        yield return CreateBlockIntent();
        yield return new MultiAttackIntent(PiercingPleasureDamage, PiercingPleasureHits);
        yield return new DetailedStatusCardIntent<PleasureCard>(
            PiercingPleasureCards, PileType.Draw, showSingleTargetMarker: false);
        yield return new HealIntent();
        yield return new BuffIntent();
        yield return CreatePleasureEgoIntent();
        yield return new BadgedAttackIntent(
            () => PleasureEgoFinalDamage,
            null,
            null,
            IntentBadge.Strength(PleasureEgoCard.Strength));
    }

    private static DefendIntent CreateBlockIntent()
    {
        return new DefendIntent();
    }

}
