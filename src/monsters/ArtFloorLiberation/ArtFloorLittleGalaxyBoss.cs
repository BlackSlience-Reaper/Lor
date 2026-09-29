using LibraryLib.Models;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.backgrounds.ArtFloorLiberation;
using LibraryOfRuina.cards.ArtFloorLiberation;
using LibraryOfRuina.combat;
using LibraryOfRuina.content.abnormalities.GalaxyChild;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.ArtFloorLiberation;
using LibraryOfRuina.visuals.ArtFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.monsters.ArtFloorLiberation;

public enum ArtFloorLittleGalaxyForm
{
    Healing,
    Crying,
    Exposed
}

public sealed class ArtFloorLittleGalaxyBoss : LiberationPhaseBossMonster
{
    private const int Phase = 3;
    private const string PartingTearsMoveId = "PARTING_TEARS";
    private const string EternalFarewellMoveId = "ETERNAL_FAREWELL";
    private const string OurLittleGalaxyMoveId = "OUR_LITTLE_GALAXY";
    private const float SegmentDelaySeconds = AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds;

    private const int PebbleHealPercent = 20;
    private const int ExposedHpCap = 50;
    private const int EternalFarewellHits = 3;
    private const int EternalFarewellStrength = 2;

    public const string Root = "res://images/monsters/art_floor/little_galaxy/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string AttackFrameS4TexturePath = Root + "attack_s4.png";
    public const string AttackFrameS3TexturePath = Root + "attack_s3.png";
    public const string AttackFrameS2TexturePath = Root + "attack_s2.png";
    public const string AttackFrameS1TexturePath = Root + "attack_s1.png";
    public const string HealingFormIconResourcePath = "powers/art_floor_little_galaxy_pebble_power.png";
    public const string CryingFormIconResourcePath = "powers/art_floor_little_galaxy_eternal_farewell_power.png";
    public const string ExposedFormIconResourcePath = "powers/art_floor_galaxy_do_not_leave_me_power.png";

    private int _baseCadenceIndex;
    private bool _isHealingForm = true;
    private bool _allFriendsDeadExposed;
    private bool _allFriendsDeadEgoUsed;
    private bool _egoQueued;
    private bool _permanentStunAfterEgo;
    private MoveState? _partingTearsState;
    private MoveState? _eternalFarewellState;
    private MoveState? _egoState;
    private MoveState? _permanentStunState;

    public override int LiberationPhase => Phase;

    public bool IsHealingForm => _isHealingForm && !_allFriendsDeadExposed;

    public bool IsExposedAfterFriendsDead => _allFriendsDeadExposed;

    public int PebbleHealPercentage => PebbleHealPercent;

    public ArtFloorLittleGalaxyForm CurrentForm =>
        _allFriendsDeadExposed
            ? ArtFloorLittleGalaxyForm.Exposed
            : IsHealingForm
                ? ArtFloorLittleGalaxyForm.Healing
                : ArtFloorLittleGalaxyForm.Crying;

    public string CurrentFormTitleKey => GetFormTitleKey(CurrentForm);

    public string CurrentFormDescriptionKey => GetFormDescriptionKey(CurrentForm);

    public string CurrentFormIconPath => GetFormIconPath(CurrentForm);

    public override bool ShouldDisappearFromDoom => false;

    public override int DefaultChaoResistance => 60;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 217, 214);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 220, 216);

    private int PartingTearsDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 16, 14);

    private int EternalFarewellDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private int OurLittleGalaxyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, ArtFloorEgoNumbers.OurLittleGalaxyUpgradedDamage, ArtFloorEgoNumbers.OurLittleGalaxyDamage);

    public override IEnumerable<string> AssetPaths =>
        ArtFloorLittleGalaxyCreatureVisuals.Profile.AssetPaths
            .Concat(new[]
            {
                GalaxyFriend.AttackSfxPath,
                GalaxyFriend.HealSfxPath,
                GetFormIconPath(ArtFloorLittleGalaxyForm.Healing),
                GetFormIconPath(ArtFloorLittleGalaxyForm.Crying),
                GetFormIconPath(ArtFloorLittleGalaxyForm.Exposed),
                ImageHelper.GetImagePath(OurLittleGalaxyEgoPreviewCard.GetPortraitResourcePath())
            })
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _baseCadenceIndex = 0;
        _isHealingForm = true;
        _allFriendsDeadExposed = false;
        _allFriendsDeadEgoUsed = false;
        _egoQueued = false;
        _permanentStunAfterEgo = false;

        if (CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        ArtFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        ArtFloorLiberationBackgroundController.SetGalaxyCryingMode(false);
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<ArtFloorErosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorLittleGalaxyEternalFarewellPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorLittleGalaxyPebblePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<UntargetablePower>(Creature);
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
        ArtFloorLiberationBackgroundController.SetGalaxyCryingMode(false);
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

    public async Task RefreshTurnStartState()
    {
        if (Creature.IsDead)
        {
            return;
        }

        if (_permanentStunAfterEgo)
        {
            await EnforcePermanentStunAfterEgo();
            return;
        }

        if (_allFriendsDeadExposed)
        {
            await QueueAllFriendsDeadEgo();
            return;
        }

        if (AllFriendsTrueDead())
        {
            await QueueAllFriendsDeadEgo();
            return;
        }

        int fakeDeadCount = CountFakeDeadFriends();
        _isHealingForm = fakeDeadCount == 0;
        ArtFloorLiberationBackgroundController.SetGalaxyCryingMode(!_isHealingForm);

        if (fakeDeadCount <= 0)
        {
            return;
        }

        MoveState? overrideState = fakeDeadCount <= 1
            ? _partingTearsState
            : _eternalFarewellState;
        if (overrideState == null)
        {
            return;
        }

        SetMoveImmediate(overrideState, forceTransition: true);
        await RefreshNodeIntents();
    }

    public async Task QueueAllFriendsDeadEgo()
    {
        if (Creature.IsDead)
        {
            return;
        }

        _isHealingForm = false;
        ArtFloorLiberationBackgroundController.SetGalaxyCryingMode(true);

        if (!_allFriendsDeadExposed)
        {
            _allFriendsDeadExposed = true;
            if (Creature.CurrentHp > ExposedHpCap)
            {
                await CreatureCmd.SetCurrentHp(Creature, ExposedHpCap);
            }

            if (Creature is LibraryCreature libraryCreature && libraryCreature.HasChaoResistance)
            {
                await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, 0m);
                libraryCreature.HealthBar?.RefreshValues();
            }

            await PowerCmdCompat.RemoveIfPresent<
                UntargetablePower>(Creature);
        }

        if (_allFriendsDeadEgoUsed || _egoState == null)
        {
            if (_permanentStunAfterEgo)
            {
                await EnforcePermanentStunAfterEgo();
            }
            return;
        }

        _egoQueued = true;
        SetMoveImmediate(_egoState, forceTransition: true);
        await RefreshNodeIntents();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        _partingTearsState = new MoveState(
            PartingTearsMoveId,
            PartingTearsMove,
            new IndiscriminateAttackIntent(() => PartingTearsDamage, null, null),
            new DebuffIntent());

        _eternalFarewellState = new MoveState(
            EternalFarewellMoveId,
            EternalFarewellMove,
            new IndiscriminateAttackIntent(() => EternalFarewellDamage, () => EternalFarewellHits, null),
            new DetailedBuffIntent<StrengthPower>(EternalFarewellStrength));

        _egoState = new MoveState(
            OurLittleGalaxyMoveId,
            OurLittleGalaxyMove,
            CreateOurLittleGalaxyEgoIntent(),
            new HealIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        _permanentStunState = new MoveState(
            stunnedMoveId,
            PermanentStunMove,
            new StunIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(_egoState, () => _egoQueued);
        chooser.AddState(_eternalFarewellState, () => CountFakeDeadFriends() > 1);
        chooser.AddState(_partingTearsState, () => CountFakeDeadFriends() > 0);
        chooser.AddState(_partingTearsState, () => _baseCadenceIndex == 0);
        chooser.AddState(_eternalFarewellState, () => true);

        _partingTearsState.FollowUpState = chooser;
        _eternalFarewellState.FollowUpState = chooser;
        _egoState.FollowUpState = chooser;
        _permanentStunState.FollowUpState = _permanentStunState;
        reviveAndEmpower.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[]
            {
                reviveAndEmpower,
                _partingTearsState,
                _eternalFarewellState,
                _egoState,
                _permanentStunState,
                chooser
            },
            chooser);
    }

    private async Task PartingTearsMove(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await ExecuteGroupAttack(PartingTearsDamage, "Attack");
        foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
        {
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target,
                amount: 3,
                turns: 1,
                Creature,
                null);
        }

        AdvanceBaseCadenceIfNoSpecialOverride();
    }

    private async Task EternalFarewellMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < EternalFarewellHits; i++)
        {
            await ExecuteGroupAttack(EternalFarewellDamage, "Attack");
        }

        await ApplySelfPowerWhileUntargetable<StrengthPower>(EternalFarewellStrength);
        AdvanceBaseCadenceIfNoSpecialOverride();
    }

    private async Task OurLittleGalaxyMove(IReadOnlyList<Creature> targets)
    {
        _egoQueued = false;
        _allFriendsDeadEgoUsed = true;

        decimal totalDamageDealt = 0m;
        for (int i = 0; i < ArtFloorEgoNumbers.OurLittleGalaxyHitCount; i++)
        {
            AttackCommand attack = await ExecuteGroupAttack(OurLittleGalaxyDamage, "Ego");
            totalDamageDealt += AttackCommandCompat.Results(attack)
                .Where(static result => result.Receiver.IsPlayer)
                .Sum(static result => result.UnblockedDamage);
        }

        if (totalDamageDealt > 0 && Creature.IsAlive)
        {
            LocalOggOneShotPlayer.Play(GalaxyFriend.HealSfxPath, -2f);
            await CreatureCmd.Heal(Creature, totalDamageDealt);
        }

        _permanentStunAfterEgo = true;
        await EnforcePermanentStunAfterEgo();
    }

    private Task PermanentStunMove(IReadOnlyList<Creature> targets)
    {
        return Task.CompletedTask;
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter is ArtFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private async Task<AttackCommand> ExecuteGroupAttack(int damage, string anim)
    {
        IReadOnlyList<Creature> players = CombatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();

        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, damage, players);

        return await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(anim, SegmentDelaySeconds)
            .AfterAttackerAnim(() =>
            {
                LocalOggOneShotPlayer.Play(GalaxyFriend.AttackSfxPath, -2f);
                return Task.CompletedTask;
            })
            .WithHitFx("vfx/vfx_attack_slash")
            .WithIndiscriminateBlockBreak(this, damage, players)
            .Execute(null);
    }

    private async Task<TPower?> ApplySelfPowerWhileUntargetable<TPower>(decimal amount)
        where TPower : PowerModel
    {
        UntargetablePower? untargetablePower = Creature.GetPower<UntargetablePower>();
        if (untargetablePower == null)
        {
            return await PowerCmdCompat.Apply<TPower>(Creature, amount, Creature, null);
        }

        untargetablePower.RemoveInternal();
        try
        {
            return await PowerCmdCompat.Apply<TPower>(Creature, amount, Creature, null);
        }
        finally
        {
            if (Creature.IsAlive
                && !IsExposedAfterFriendsDead)
            {
                await PowerCmdCompat.Ensure<
                    UntargetablePower>(Creature);
            }
        }
    }

    private async Task EnforcePermanentStunAfterEgo()
    {
        if (!_permanentStunAfterEgo || Creature.IsDead)
        {
            return;
        }

        _egoQueued = false;
        _allFriendsDeadEgoUsed = true;
        _allFriendsDeadExposed = true;
        _isHealingForm = false;
        ArtFloorLiberationBackgroundController.SetGalaxyCryingMode(true);

        await PowerCmdCompat.RemoveIfPresent<
            UntargetablePower>(Creature);

        if (Creature is LibraryCreature libraryCreature && libraryCreature.HasChaoResistance)
        {
            if (libraryCreature.CurrentChaoValue != 0)
            {
                await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, 0m);
            }

            await LibraryCreatureCmd.Stun(libraryCreature, stunnedMoveId);
            libraryCreature.RestoreChaoOnNextOwnerTurn = false;
            libraryCreature.HealthBar?.RefreshValues();
        }
        else
        {
            await CreatureCmd.Stun(Creature, stunnedMoveId);
        }

        if (_permanentStunState != null)
        {
            SetMoveImmediate(_permanentStunState, forceTransition: true);
        }
    }

    private bool AllFriendsTrueDead()
    {
        ArtFloorGalaxyFriend[] friends = ArtFloorGalaxyFriend.GetFriends(Creature.CombatState)
            .ToArray();
        return friends.Length > 0
            && friends.All(static friend => !friend.Creature.IsAlive && !friend.IsFakeDead);
    }

    private int CountFakeDeadFriends()
    {
        return ArtFloorGalaxyFriend.GetFriends(Creature.CombatState)
            .Count(static friend => friend.IsFakeDead);
    }

    private void AdvanceBaseCadenceIfNoSpecialOverride()
    {
        if (_allFriendsDeadExposed || CountFakeDeadFriends() > 0)
        {
            return;
        }

        _baseCadenceIndex = (_baseCadenceIndex + 1) % 2;
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

    private PlayCardAttackIntent<OurLittleGalaxyEgoPreviewCard> CreateOurLittleGalaxyEgoIntent()
    {
        return new PlayCardAttackIntent<OurLittleGalaxyEgoPreviewCard>(
            "OUR_LITTLE_GALAXY_EGO_PREVIEW_CARD",
            () => OurLittleGalaxyDamage,
            static (card, _) => card.UpgradePreview(),
            () => ArtFloorEgoNumbers.OurLittleGalaxyHitCount,
            badges: [IndiscriminateAttackIntent.CreateGroupAttackBadge()]);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new IndiscriminateAttackIntent(() => PartingTearsDamage, null, null);
        yield return new DebuffIntent();
        yield return new IndiscriminateAttackIntent(() => EternalFarewellDamage, () => EternalFarewellHits, null);
        yield return new DetailedBuffIntent<StrengthPower>(EternalFarewellStrength);
        yield return CreateOurLittleGalaxyEgoIntent();
        yield return new HealIntent();
        yield return new BuffIntent();
    }

    public static string GetFormTitleKey(ArtFloorLittleGalaxyForm form)
    {
        return form switch
        {
            ArtFloorLittleGalaxyForm.Healing => "ART_FLOOR_LITTLE_GALAXY_BOSS.forms.HEALING.title",
            ArtFloorLittleGalaxyForm.Crying => "ART_FLOOR_LITTLE_GALAXY_BOSS.forms.CRYING.title",
            ArtFloorLittleGalaxyForm.Exposed => "ART_FLOOR_LITTLE_GALAXY_BOSS.forms.EXPOSED.title",
            _ => "ART_FLOOR_LITTLE_GALAXY_BOSS.forms.HEALING.title"
        };
    }

    public static string GetFormDescriptionKey(ArtFloorLittleGalaxyForm form)
    {
        return form switch
        {
            ArtFloorLittleGalaxyForm.Healing => "ART_FLOOR_LITTLE_GALAXY_BOSS.forms.HEALING.description",
            ArtFloorLittleGalaxyForm.Crying => "ART_FLOOR_LITTLE_GALAXY_BOSS.forms.CRYING.description",
            ArtFloorLittleGalaxyForm.Exposed => "ART_FLOOR_LITTLE_GALAXY_BOSS.forms.EXPOSED.description",
            _ => "ART_FLOOR_LITTLE_GALAXY_BOSS.forms.HEALING.description"
        };
    }

    public static string GetFormIconPath(ArtFloorLittleGalaxyForm form)
    {
        string resourcePath = form switch
        {
            ArtFloorLittleGalaxyForm.Healing => HealingFormIconResourcePath,
            ArtFloorLittleGalaxyForm.Crying => CryingFormIconResourcePath,
            ArtFloorLittleGalaxyForm.Exposed => ExposedFormIconResourcePath,
            _ => HealingFormIconResourcePath
        };

        return ImageHelper.GetImagePath(resourcePath);
    }

}
