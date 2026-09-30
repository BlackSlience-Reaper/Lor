using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.events.WarpTrain;

public sealed class Tomerry : MonsterModel
{
    private const int LetsPlayHits = 2;
    private const int LetsPlayBlock = 11;

    private const int WouldaSquareHits = 2;
    private const int WouldaSquareBlock = 16;
    private const int WouldaSquareNextTurnStrength = 5;
    private const int WouldaSquareSelfVulnerable = 3;

    private const int TriangleHits = 3;
    private const int TriangleDamage = 14;

    private const int LoveTownBlock = 9;
    private const int LoveTownNextTurnStrength = 5;

    private const int RuckusHits = 3;

    private bool _isPhaseTwo;
    private int _transitionStunTurns;

    private MoveState? _stunnedState;
    private MoveState? _loveTownWelcomesAllState;

    public override int MinInitialHp => 680;

    public override int MaxInitialHp => 700;

    public override IEnumerable<string> AssetPaths =>
        TomerryCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private int LetsPlayDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8);

    private int WouldaSquareDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    private int LoveTownDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 13);

    private int RuckusDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 7);

    public bool IsPhaseTwo => _isPhaseTwo;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        _isPhaseTwo = false;
        _transitionStunTurns = 0;

        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<LibraryOfRuinaLastLovePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LibraryOfRuinaFaintMemoriesPower>(Creature, 3m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var letsPlay1 = new MoveState(
            "LETS_PLAY_1",
            LetsPlayMove,
            new MultiAttackIntent(LetsPlayDamage, LetsPlayHits),
            new DefendIntent());

        var letsPlay2 = new MoveState(
            "LETS_PLAY_2",
            LetsPlayMove,
            new MultiAttackIntent(LetsPlayDamage, LetsPlayHits),
            new DefendIntent());

        var letsPlay3 = new MoveState(
            "LETS_PLAY_3",
            LetsPlayMove,
            new MultiAttackIntent(LetsPlayDamage, LetsPlayHits),
            new DefendIntent());

        var wouldaSquare = new MoveState(
            "WOULDA_SQUARE_LOOK_NICE",
            WouldaSquareMove,
            new MultiAttackIntent(WouldaSquareDamage, WouldaSquareHits),
            new DefendIntent(),
            new BuffIntent(),
            new DebuffIntent(strong: true));

        var triangle = new MoveState(
            "TRIANGLE_SOUNDS_BETTER",
            TriangleSoundsBetterMove,
            new MultiAttackIntent(TriangleDamage, TriangleHits));

        var loveTownWelcomesAll = new MoveState(
            "LOVE_TOWN_WELCOMES_ALL",
            LoveTownWelcomesAllMove,
            new SingleAttackIntent(LoveTownDamage),
            new DefendIntent(),
            new BuffIntent());

        var ruckus = new MoveState(
            "RUCKUS",
            RuckusMove,
            new MultiAttackIntent(RuckusDamage, RuckusHits));

        var stunned = new MoveState(
            stunnedMoveId,
            StunnedMove,
            new StunIntent());

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(stunned, () => _isPhaseTwo && _transitionStunTurns > 0);
        chooser.AddState(loveTownWelcomesAll, () => _isPhaseTwo);
        chooser.AddState(letsPlay1, () => true);

        letsPlay1.FollowUpState = letsPlay2;
        letsPlay2.FollowUpState = letsPlay3;
        letsPlay3.FollowUpState = wouldaSquare;
        wouldaSquare.FollowUpState = triangle;
        triangle.FollowUpState = chooser;

        loveTownWelcomesAll.FollowUpState = ruckus;
        ruckus.FollowUpState = chooser;
        stunned.FollowUpState = chooser;

        states.Add(letsPlay1);
        states.Add(letsPlay2);
        states.Add(letsPlay3);
        states.Add(wouldaSquare);
        states.Add(triangle);
        states.Add(loveTownWelcomesAll);
        states.Add(ruckus);
        states.Add(stunned);
        states.Add(chooser);

        _stunnedState = stunned;
        _loveTownWelcomesAllState = loveTownWelcomesAll;

        return new MonsterMoveStateMachine(states, chooser);
    }

    public async Task EnterPhaseTwo(int stunTurns)
    {
        bool wasPhaseTwo = _isPhaseTwo;
        _isPhaseTwo = true;
        _transitionStunTurns = 0;

        if (!wasPhaseTwo
            && CombatQueries.CreatureNodeOf(this)?.Visuals is TomerryCreatureVisuals visuals)
        {
            visuals.SetPhaseTwo();
        }

        bool shouldStun = !wasPhaseTwo && stunTurns > 0;
        if (shouldStun)
        {
            string nextMoveId = _loveTownWelcomesAllState?.Id ?? NextMove.Id;
            await CreatureCmd.Stun(Creature, nextMoveId);
        }
        else if (_loveTownWelcomesAllState != null)
        {
            SetMoveImmediate(_loveTownWelcomesAllState, forceTransition: true);
        }

        NCreature? creatureNode = CombatQueries.CreatureNodeOf(this);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

    private async Task LetsPlayMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(LetsPlayDamage)
            .FromMonster(this)
            .WithHitCount(LetsPlayHits)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        await CreatureCmd.GainBlock(Creature, LetsPlayBlock, ValueProp.Move, null);
    }

    private async Task WouldaSquareMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.525f);
        await CreatureCmd.GainBlock(Creature, WouldaSquareBlock, ValueProp.Move, null);
        await DamageCmd.Attack(WouldaSquareDamage)
            .FromMonster(this)
            .WithHitCount(WouldaSquareHits)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, WouldaSquareNextTurnStrength, Creature, null);
        await PowerCmdCompat.Apply<VulnerablePower>(Creature, WouldaSquareSelfVulnerable, Creature, null);
    }

    private async Task TriangleSoundsBetterMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(TriangleDamage)
            .FromMonster(this)
            .WithHitCount(TriangleHits)
            .WithAttackerAnim("TriangleSoundsBetter", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task LoveTownWelcomesAllMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(LoveTownDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        await CreatureCmd.GainBlock(Creature, LoveTownBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, LoveTownNextTurnStrength, Creature, null);
    }

    private async Task RuckusMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(RuckusDamage)
            .FromMonster(this)
            .WithHitCount(RuckusHits)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private Task StunnedMove(IReadOnlyList<Creature> targets)
    {
        if (_transitionStunTurns > 0)
        {
            _transitionStunTurns--;
        }

        return Task.CompletedTask;
    }
}
