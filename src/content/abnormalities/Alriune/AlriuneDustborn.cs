using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.content.abnormalities.Alriune;

public sealed class AlriuneDustborn : LorMonsterModel
{
    private const int AllMovesMask = 0b111; // 人偶：一轮包含三种招式，各占一个标志位。

    [SavedProperty]
    public int RemainingMoves { get; set; } = AllMovesMask;

    [SavedProperty]
    public int LastMove { get; set; } = -1;

    [SavedProperty]
    public int PlannedMove { get; set; } = -1;

    [SavedProperty]
    public bool AwaitingRevival { get; set; }

    private MoveState? _reviveState;

    public override bool IsHealthBarVisible => !IsMutable || !AwaitingRevival;

    internal bool HasLivingAlriune => CombatState?.Enemies
        .Any(static enemy => enemy.IsAlive && enemy.Monster is Alriune) == true;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        // 仅复制招式袋与游标，入场时重新建立绑定当前人偶的招式委托。
        ResetStateMachine();
        _reviveState = null;
    }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            AlriuneNumbers.DustbornHighMinHp,
            AlriuneNumbers.DustbornMinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            AlriuneNumbers.DustbornHighMaxHp,
            AlriuneNumbers.DustbornMaxHp);

    public override int DefaultChaoResistance => AlriuneNumbers.DustbornMaxChao;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        new(LibraryResistanceLevel.Endure);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override IEnumerable<string> AssetPaths => AlriuneAssets.DustbornAssets
        .Concat(CreateIntents().SelectMany(static intent => intent.AssetPaths))
        .Distinct();

    private static int FlowerDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            AlriuneNumbers.FlowerBushHighDamage,
            AlriuneNumbers.FlowerBushDamage);

    private static int DespairDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            AlriuneNumbers.DespairHighDamage,
            AlriuneNumbers.DespairDamage);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Ensure<MinionPower>(Creature);
        await PowerCmdCompat.Ensure<AlriuneDustToDustPower>(Creature);
        await PowerCmdCompat.Ensure<AlriuneClayDollPower>(Creature);
        PlanNextMove();
        if (AwaitingRevival)
        {
            ShowRevivalIntent();
        }
    }

    private AbstractIntent[] CreateIntents() =>
    [
        new CombinedAttackCardDebuffIntent(() => FlowerDamage, null, "ALRIUNE_FLOWER_BUSH.description",
            IntentBadge.StatusCard<Slimed>(AlriuneNumbers.FlowerBushSlimed)),
        new BadgedDebuffIntent(IntentBadge.FromPower<WeakPower>(AlriuneNumbers.MindCrackWeak),
            AlriuneNumbers.MindCrackWeak, "ALRIUNE_MIND_CRACK.description"),
        new AlriuneMultiAttackIntent(DespairDamage, AlriuneNumbers.DespairHits, "ALRIUNE_DESPAIR.description"),
        new HealIntent()
    ];

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        AbstractIntent[] intents = CreateIntents();
        MoveState flower = new("FLOWER_BUSH", FlowerBush, intents[0]);
        MoveState mind = new("MIND_CRACK", MindCrack, intents[1]);
        MoveState despair = new("SPREADING_DESPAIR", Despair, intents[2]);
        MoveState rest = _reviveState = new LibraryPhaseTransitionMoveState("REVIVAL_REST", Revive, intents[3])
        {
            MustPerformOnceBeforeTransitioning = true
        };
        ConditionalBranchState chooser = new("DUSTBORN_ROUTER");
        chooser.AddState(rest, () => AwaitingRevival);
        chooser.AddState(flower, () => PlannedMove <= 0);
        chooser.AddState(mind, () => PlannedMove == 1);
        chooser.AddState(despair, () => true);
        flower.FollowUpState = chooser;
        mind.FollowUpState = chooser;
        despair.FollowUpState = chooser;
        rest.FollowUpState = chooser;
        return new MonsterMoveStateMachine([flower, mind, despair, rest, chooser], chooser);
    }

    // 条件分支也会被招式图预览查询，随机选择只在入场及完成招式时执行。
    private void PlanNextMove()
    {
        if (!IsMutable || PlannedMove >= 0)
        {
            return;
        }
        int[] candidates = Enumerable.Range(0, 3)
            .Where(move => (RemainingMoves & (1 << move)) != 0
                && (RemainingMoves != AllMovesMask || move != LastMove))
            .ToArray();
        PlannedMove = candidates[RunRng.MonsterAi.NextInt(candidates.Length)];
    }

    private void CompleteMove(int move)
    {
        RemainingMoves &= ~(1 << move);
        if (RemainingMoves == 0)
        {
            RemainingMoves = AllMovesMask;
        }
        LastMove = move;
        PlannedMove = -1;
        if (Creature.IsAlive)
        {
            PlanNextMove();
        }
    }

    private async Task FlowerBush(IReadOnlyList<Creature> targets)
    {
        await AttackPlayers(FlowerDamage, "Pierce");
        await CardPileCmdCompat.AddToCombatAndPreview<Slimed>(
            CombatState.PlayerCreatures.Where(static player => player.IsAlive).ToArray(),
            PileType.Draw, AlriuneNumbers.FlowerBushSlimed, addedByPlayer: false, CardPilePosition.Random);
        CompleteMove(0);
    }

    private async Task MindCrack(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Dodge", AlriuneNumbers.PoseSeconds);
        await PowerCmdCompat.Apply<WeakPower>(CombatState.PlayerCreatures.Where(static player => player.IsAlive).ToArray(),
            AlriuneNumbers.MindCrackWeak, Creature, null);
        CompleteMove(1);
    }

    private async Task Despair(IReadOnlyList<Creature> targets)
    {
        for (int hit = 0; hit < AlriuneNumbers.DespairHits && Creature.IsAlive; hit++)
        {
            await AttackPlayers(DespairDamage, "Slash");
        }
        CompleteMove(2);
    }

    private async Task AttackPlayers(int damage, string animation)
    {
        LocalOggOneShotPlayer.Play(AlriuneAssets.DustbornAttackSfx, AlriuneNumbers.SfxVolumeDb);
        await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(animation, AlriuneNumbers.PoseSeconds)
            .Execute(null);
    }

    internal async Task EnterFakeDeath()
    {
        if (AwaitingRevival || !HasLivingAlriune)
        {
            return;
        }
        AwaitingRevival = true;
        await FakeDeathDebuffHelper.ClearNonPassivePowers(Creature,
            static power => power is MinionPower);
        ShowRevivalIntent();
    }

    private void ShowRevivalIntent()
    {
        if (_reviveState != null)
        {
            SetMoveImmediate(_reviveState, forceTransition: true);
        }
        NCombatRoom.Instance?.SetCreatureIsInteractable(Creature, false);
    }

    private async Task Revive(IReadOnlyList<Creature> targets)
    {
        if (!AwaitingRevival || !HasLivingAlriune)
        {
            return;
        }
        // 同一个 Creature 与 NCreature 原地恢复，这次行动只执行复活。
        if (Creature is LibraryCreature libraryCreature)
        {
            libraryCreature.RestoreChaoOnNextOwnerTurn = false;
            if (libraryCreature.IsChaoed)
            {
                libraryCreature.RestorePreStunResistance();
            }
            await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, libraryCreature.MaxChaoValue);
        }
        AwaitingRevival = false;
        await CreatureCmd.SetCurrentHp(Creature, Creature.MaxHp);
        await PowerCmdCompat.Ensure<MinionPower>(Creature);
        await PowerCmdCompat.Ensure<AlriuneClayDollPower>(Creature);
        NCombatRoom.Instance?.SetCreatureIsInteractable(Creature, true);
        PlanNextMove();
        LocalOggOneShotPlayer.Play(AlriuneAssets.DustbornDodgeSfx, AlriuneNumbers.SfxVolumeDb);
        await CreatureCmd.TriggerAnim(Creature, "Dodge", AlriuneNumbers.PoseSeconds);
    }
}
