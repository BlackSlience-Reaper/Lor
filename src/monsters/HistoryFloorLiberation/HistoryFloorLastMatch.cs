using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.visuals.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.HistoryFloorLiberation;

public sealed class HistoryFloorLastMatch : CounterIntentMonsterModel
{
    private const string EmberMoveId = "EMBER";
    private const string BrokenHopeMoveId = "BROKEN_HOPE";
    private const string IgniteMoveId = "IGNITE";
    private const int EmberHits = 2;
    private const int EmberBurn = 2;

    public override int DefaultChaoResistance => 25;

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Blunt = LibraryResistanceLevel.Vulnerable,
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Blunt = LibraryResistanceLevel.Vulnerable,
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal
    };

    private const int BrokenHopeBlock = 7;
    private const int BrokenHopeGuard = 2;
    private const int BrokenHopeGuardTurns = 2;
    private const int IgniteBurn = 2;

    public const string IdleTexturePath = "res://images/monsters/history_floor/last_match.webp";
    public const string AttackTexturePath = "res://images/monsters/history_floor/last_match_attack.png";
    public const string HitTexturePath = "res://images/monsters/history_floor/last_match_hit.png";
    public const string CastTexturePath = "res://images/monsters/history_floor/last_match_cast.png";
    private const string MatchAttackSfxPath = "res://audio/sfx/scorched_girl/fourth_match_flame_attack.ogg";

    private static readonly int[][] MovePatterns =
    [
        [1, 2, 3],
        [2, 1, 3],
        [3, 2, 1],
        [2, 3, 1]
    ];

    private int _patternIndex;
    private int _moveIndex;

    private int[] CurrentPattern => MovePatterns[Math.Clamp(_patternIndex, 0, MovePatterns.Length - 1)];

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 51, 48);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 53, 50);

    private int EmberDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 2, 1);

    private int IgniteDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 4, 3);

    public override IEnumerable<string> AssetPaths =>
        HistoryFloorLastMatchCreatureVisuals.Profile.AssetPaths
            .Append(MatchAttackSfxPath)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    internal void ConfigurePattern(int patternIndex)
    {
        AssertMutable();
        _patternIndex = Math.Clamp(patternIndex, 0, MovePatterns.Length - 1);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _moveIndex = 0;
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var ember = new MoveState(
            EmberMoveId,
            EmberMove,
            new MultiAttackIntent(EmberDamage, EmberHits),
            new DebuffIntent());

        var brokenHope = new MoveState(
            BrokenHopeMoveId,
            BrokenHopeMove,
            new DefendIntent(),
            new BuffIntent());

        var ignite = new MoveState(
            IgniteMoveId,
            IgniteMove,
            new SingleAttackIntent(IgniteDamage),
            new DebuffIntent(strong: true));

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(ember, () => CurrentMoveNumber() == 1);
        chooser.AddState(brokenHope, () => CurrentMoveNumber() == 2);
        chooser.AddState(ignite, () => true);

        ember.FollowUpState = chooser;
        brokenHope.FollowUpState = chooser;
        ignite.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[] { ember, brokenHope, ignite, chooser },
            chooser);
    }

    private async Task EmberMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < EmberHits; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(MatchAttackSfxPath, -3f);

            await DamageCmd.Attack(EmberDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await PowerCmdCompat.Apply<LibraryBurnPower>(
            targets.Where(static target => target.IsAlive),
            EmberBurn,
            Creature,
            null);

        AdvanceMove();
    }

    private async Task BrokenHopeMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);
        await CreatureCmd.GainBlock(Creature, BrokenHopeBlock, ValueProp.Move, null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            Creature,
            BrokenHopeGuard,
            BrokenHopeGuardTurns,
            Creature,
            null);

        AdvanceMove();
    }

    private async Task IgniteMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(MatchAttackSfxPath, -3f);

        await DamageCmd.Attack(IgniteDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await PowerCmdCompat.Apply<LibraryBurnPower>(
            targets.Where(static target => target.IsAlive),
            IgniteBurn,
            Creature,
            null);

        AdvanceMove();
    }

    private int CurrentMoveNumber() => CurrentPattern[_moveIndex % CurrentPattern.Length];

    private void AdvanceMove()
    {
        _moveIndex++;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new MultiAttackIntent(EmberDamage, EmberHits);
        yield return new DebuffIntent();
        yield return new DefendIntent();
        yield return new BuffIntent();
        yield return new SingleAttackIntent(IgniteDamage);
        yield return new DebuffIntent(strong: true);
    }
}
