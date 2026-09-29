using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.visuals.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.HistoryFloorLiberation;

public sealed class HistoryFloorVineBarrier : LorMonsterModel
{
    private const string PoisonStingBarrierMoveId = "POISON_STING_BARRIER";

    private const int BlockAmount = 20;
    private const int ThornsAmount = 2;

    public override int DefaultChaoResistance => 70;

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public const string Root = "res://images/monsters/history_floor/emerald_bough/";
    public const string IdleTexturePath = Root + "vine_barrier_idle.png";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 72, 60);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 75, 63);

    public override IEnumerable<string> AssetPaths =>
        HistoryFloorVineBarrierCreatureVisuals.Profile.AssetPaths
        .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
        .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Apply<EmeraldBoughStranglingVinePower>(Creature, 1m, Creature, null, silent: true);
        var stranglingVine = Creature.GetPower<EmeraldBoughStranglingVinePower>();
        stranglingVine?.SetAmount(0, silent: true);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<PlatingPower>(Creature, 20m, Creature, null, true);
        await PowerCmdCompat.Apply<BarricadePower>(Creature, 1m, Creature, null, true);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature || wasRemovalPrevented)
        {
            return;
        }

        if (Creature.CombatState is not { } combatState)
        {
            return;
        }

        HistoryFloorEmeraldBoughBoss? boss = combatState.Enemies
            .Select(static enemy => enemy.Monster as HistoryFloorEmeraldBoughBoss)
            .FirstOrDefault(static b => b != null);
        if (boss == null)
        {
            return;
        }

        await boss.OnVineBarrierDestroyed();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var poisonSting = new MoveState(
            PoisonStingBarrierMoveId,
            PoisonStingBarrierMove,
            new DefendIntent(),
            new DetailedBuffIntent<ThornsPower>(ThornsAmount));

        poisonSting.FollowUpState = poisonSting;

        return new MonsterMoveStateMachine(
            new MonsterState[] { poisonSting },
            poisonSting);
    }

    private async Task PoisonStingBarrierMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Defend", 0.4f);
        await CreatureCmd.GainBlock(Creature, BlockAmount, ValueProp.Move, null);
        await PowerCmdCompat.Apply<ThornsPower>(Creature, ThornsAmount, Creature, null);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new DefendIntent();
        yield return new DetailedBuffIntent<ThornsPower>(ThornsAmount);
    }
}
