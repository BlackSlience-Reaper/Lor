using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.DeadButterfly;

internal enum DeadButterflyInitialMove
{
    AngryRelease = 1,
    SpiritRelease = 2,
    PainfulRelease = 3,
    PeacefulRepose = 4
}

public sealed class DeadButterfly : LorMonsterModel
{
    public override int DefaultChaoResistance => 10;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    private const float AnimationDurationScale = 2.25f;
    public const string AngryReleaseMoveId = "ANGRY_RELEASE";
    public const string SpiritReleaseMoveId = "SPIRIT_RELEASE";
    public const string PainfulReleaseMoveId = "PAINFUL_RELEASE";
    public const string PeacefulReposeMoveId = "PEACEFUL_REPOSE";

    public const string IdleTexturePath = "res://images/monsters/dead_butterfly/idle.png";
    public const string AttackTexturePath = "res://images/monsters/dead_butterfly/attack.png";
    public const string HitTexturePath = "res://images/monsters/dead_butterfly/hit.png";
    public const string AttackSfxPath = "res://audio/sfx/dead_butterfly/dead_butterfly_attack.ogg";
    public const string DodgeSfxPath = "res://audio/sfx/dead_butterfly/dead_butterfly_dodge.ogg";

    private static readonly string BookRelicTitleLocKey =
        $"{ModelDb.GetId<DeadButterfliesBookRelic>().Entry}.title";

    private List<DeadButterflyInitialMove> _sequence =
    [
        DeadButterflyInitialMove.AngryRelease,
        DeadButterflyInitialMove.SpiritRelease,
        DeadButterflyInitialMove.PainfulRelease,
        DeadButterflyInitialMove.PeacefulRepose
    ];

    private MoveState _move1 = null!;
    private MoveState _move2 = null!;
    private MoveState _move3 = null!;
    private MoveState _move4 = null!;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 16, 13);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 18, 15);

    private int LightDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private int SpiritDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 2, 1);

    private int HeavyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                DeadButterflyCreatureVisuals.Profile.AssetPaths)
            {
                AttackSfxPath,
                DodgeSfxPath
            };

            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return;
        }

        if (creature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || room.Encounter is not DeadButterflyWeak and not DeadButterflyStrong)
        {
            return;
        }

        foreach (var player in room.CombatState.Players)
        {
            if (player.GetRelic<DeadButterfliesBookRelic>() != null
                || AbnormalityPageRewardHelper.HasRelicReward(room, player, BookRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<DeadButterfliesBookRelic>().ToMutable(), player));
        }
    }

    internal void ConfigureMoveSequence(params DeadButterflyInitialMove[] sequence)
    {
        AssertMutable();
        if (sequence.Length > 0)
        {
            _sequence.Clear();
            _sequence.AddRange(sequence);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _move1 = new MoveState(
            AngryReleaseMoveId,
            AngryReleaseMove,
            new SingleAttackIntent(LightDamage),
            new DebuffIntent());

        _move2 = new MoveState(
            SpiritReleaseMoveId,
            SpiritReleaseMove,
            new SingleAttackIntent(SpiritDamage),
            new DefendIntent());

        _move3 = new MoveState(
            PainfulReleaseMoveId,
            PainfulReleaseMove,
            new DefendIntent(),
            new DebuffIntent());

        _move4 = new MoveState(
            PeacefulReposeMoveId,
            PeacefulReposeMove,
            new MultiAttackIntent(HeavyDamage, 2));

        LinkConfiguredMoveSequence();
        MonsterState initialState = StateFor(_sequence[0]);

        return new MonsterMoveStateMachine([_move1, _move2, _move3, _move4], initialState);
    }

    private MoveState StateFor(DeadButterflyInitialMove move) => move switch
    {
        DeadButterflyInitialMove.SpiritRelease => _move2,
        DeadButterflyInitialMove.PainfulRelease => _move3,
        DeadButterflyInitialMove.PeacefulRepose => _move4,
        _ => _move1
    };

    private void LinkConfiguredMoveSequence()
    {
        if (_sequence.Count == 0)
        {
            _sequence.Add(DeadButterflyInitialMove.AngryRelease);
        }

        for (int i = 0; i < _sequence.Count; i++)
        {
            MoveState current = StateFor(_sequence[i]);
            MoveState next = StateFor(_sequence[(i + 1) % _sequence.Count]);
            current.FollowUpState = next;
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _sequence = new List<DeadButterflyInitialMove>(_sequence);
    }

    private async Task AngryReleaseMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await ExecuteAttackSegment(LightDamage);

        if (targets.Count > 0)
        {
            await PowerCmdCompat.Apply<WeakPower>(targets, 1m, Creature, null);
        }
    }

    private async Task SpiritReleaseMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await ExecuteAttackSegment(SpiritDamage);
        await CreatureCmd.GainBlock(Creature, 9, ValueProp.Move, null);
    }

    private async Task PainfulReleaseMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", ScaleDuration(0.05f));
        await CreatureCmd.GainBlock(Creature, 6, ValueProp.Move, null);

        if (targets.Count > 0)
        {
            await PowerCmdCompat.Apply<FrailPower>(targets, 1m, Creature, null);
        }
    }

    private async Task PeacefulReposeMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < 2; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            await ExecuteAttackSegment(HeavyDamage);
            await Cmd.CustomScaledWait(ScaleDuration(0.04f), ScaleDuration(0.08f));
        }
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new SingleAttackIntent(LightDamage);
        yield return new SingleAttackIntent(SpiritDamage);
        yield return new DefendIntent();
        yield return new DebuffIntent();
        yield return new MultiAttackIntent(HeavyDamage, 2);
    }

    private static float ScaleDuration(float seconds) => seconds * AnimationDurationScale;

    private Task ExecuteAttackSegment(int damage)
    {
        return AbnormalityAnimHelper.ExecuteAttackSegment(
            this,
            damage,
            delaySeconds: ScaleDuration(AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds));
    }
}
