using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.ArtFloorLiberation;
using LibraryOfRuina.visuals.ArtFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.ArtFloorLiberation;

public enum ArtFloorDustbornSide
{
    Left,
    Right
}

public sealed class ArtFloorDustbornPerson : LibraryMonsterModel, ILiberationPhaseBoss
{
    private const int Phase = 5;
    private const string FlowerBushMoveId = "FLOWER_BUSH";
    private const string MindCrackMoveId = "MIND_CRACK";
    private const string SpreadingDespairMoveId = "SPREADING_DESPAIR";
    internal const string WinterStasisMoveId = "WINTER_STASIS";
    private const float SegmentDelaySeconds = AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds;

    public const int FlowerBushBlock = 18;
    public const int FlowerBushFragrance = 1;
    public const int CollapseAmount = 1;
    public const int DespairConfusion = 1;

    public const string Root = ArtFloorNostalgicScentBoss.Root;
    public const string IdleTexturePath = Root + "dustborn_idle.png";
    public const string PierceTexturePath = Root + "dustborn_pierce.png";
    public const string SlashTexturePath = Root + "dustborn_slash.png";
    public const string HitTexturePath = Root + "dustborn_hit.png";
    public const string DodgeTexturePath = Root + "dustborn_dodge.png";

    public const string AttackSfxPath = ArtFloorNostalgicScentBoss.SfxRoot + "dustborn_attack.ogg";
    public const string DodgeSfxPath = ArtFloorNostalgicScentBoss.SfxRoot + "dustborn_dodge.ogg";

    private ArtFloorDustbornSide _side = ArtFloorDustbornSide.Left;
    private int _cadenceIndex;
    private MoveState? _flowerBushState;
    private MoveState? _mindCrackState;
    private MoveState? _spreadingDespairState;
    private MoveState? _winterStasisState;

    public int LiberationPhase => Phase;

    public override bool ShouldDisappearFromDoom => false;

    public override int DefaultChaoResistance => 200;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 167, 142);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 170, 144);

    private int FlowerBushDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8);

    private int SpreadingDespairDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 7);

    public override IEnumerable<string> AssetPaths =>
        ArtFloorDustbornPersonCreatureVisuals.Profile.AssetPaths
            .Concat(new[]
            {
                AttackSfxPath,
                DodgeSfxPath
            })
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public void ConfigureSide(ArtFloorDustbornSide side)
    {
        _side = side;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _cadenceIndex = 0;

        if (CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
        }

        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<ArtFloorClayDollPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorDustToDustPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public Task TriggerReviveAndEmpowerState() => Task.CompletedTask;

    public void ForceReviveAndEmpowerState()
    {
    }

    public Task QueueWinterStasis()
    {
        if (Creature.IsDead || _winterStasisState == null)
        {
            return Task.CompletedTask;
        }

        SetMoveImmediate(_winterStasisState, forceTransition: true);
        return RefreshNodeIntents();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _flowerBushState = new MoveState(
            FlowerBushMoveId,
            FlowerBushMove,
            new CombinedAttackDefendIntent(
                () => FlowerBushDamage,
                null,
                null,
                FlowerBushBlock,
                IntentBadge.FromPower<ArtFloorFragrancePower>(FlowerBushFragrance)));

        _mindCrackState = new MoveState(
            MindCrackMoveId,
            MindCrackMove,
            new DebuffIntent());

        _spreadingDespairState = new MoveState(
            SpreadingDespairMoveId,
            SpreadingDespairMove,
            new CombinedAttackDebuffIntent(
                () => SpreadingDespairDamage,
                null,
                null,
                IntentBadge.Confusion(DespairConfusion)));

        _winterStasisState = new MoveState(
            WinterStasisMoveId,
            WinterStasisMove,
            new StunIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(_winterStasisState, () => Creature.HasPower<ArtFloorDustbornWinterStasisPower>());
        chooser.AddState(_flowerBushState, () => ResolveCadenceMoveId() == FlowerBushMoveId);
        chooser.AddState(_mindCrackState, () => ResolveCadenceMoveId() == MindCrackMoveId);
        chooser.AddState(_spreadingDespairState, () => true);

        _flowerBushState.FollowUpState = chooser;
        _mindCrackState.FollowUpState = chooser;
        _spreadingDespairState.FollowUpState = chooser;
        _winterStasisState.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[]
            {
                _flowerBushState,
                _mindCrackState,
                _spreadingDespairState,
                _winterStasisState,
                chooser
            },
            chooser);
    }

    private async Task FlowerBushMove(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await ExecuteGroupAttack(FlowerBushDamage, "Pierce", "vfx/vfx_dramatic_stab", AttackSfxPath);
        foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
        {
            await PowerCmdCompat.ApplyDebuff<ArtFloorFragrancePower>(
                target,
                FlowerBushFragrance,
                Creature,
                null);
        }

        await CreatureCmd.GainBlock(Creature, FlowerBushBlock, ValueProp.Move, null);
        AdvanceCadence();
    }

    private async Task MindCrackMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> players = CombatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();
        foreach (Creature player in players)
        {
            await PowerCmdCompat.Apply<ArtFloorNextTurnCollapsePower>(
                player,
                CollapseAmount,
                Creature,
                null);
        }

        AdvanceCadence();
    }

    private async Task SpreadingDespairMove(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await ExecuteGroupAttack(SpreadingDespairDamage, "Slash", "vfx/vfx_attack_slash", AttackSfxPath);
        foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
        {
            await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaConfusionPower>(
                target,
                DespairConfusion,
                Creature,
                null);
        }

        AdvanceCadence();
    }

    private Task WinterStasisMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(DodgeSfxPath, -2f);
        return CreatureCmd.TriggerAnim(Creature, "Dodge", 0.2f);
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

    private string ResolveCadenceMoveId()
    {
        return _side == ArtFloorDustbornSide.Left
            ? _cadenceIndex switch
            {
                0 => FlowerBushMoveId,
                1 => MindCrackMoveId,
                _ => SpreadingDespairMoveId
            }
            : _cadenceIndex switch
            {
                0 => MindCrackMoveId,
                1 => SpreadingDespairMoveId,
                _ => FlowerBushMoveId
            };
    }

    private void AdvanceCadence()
    {
        _cadenceIndex = (_cadenceIndex + 1) % 3;
    }

    private Task RefreshNodeIntents()
    {
        return NCombatRoom.Instance?.GetCreatureNode(Creature)?.RefreshIntents() ?? Task.CompletedTask;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new CombinedAttackDefendIntent(
            () => FlowerBushDamage,
            null,
            null,
            FlowerBushBlock,
            IntentBadge.FromPower<ArtFloorFragrancePower>(FlowerBushFragrance));
        yield return new DefendIntent();
        yield return new DebuffIntent();
        yield return new CombinedAttackDebuffIntent(
            () => SpreadingDespairDamage,
            null,
            null,
            IntentBadge.Confusion(DespairConfusion));
        yield return new StunIntent();
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
