using LibraryOfRuina.interop;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.NaturalFloorLiberation;

public abstract class NaturalFloorMagicalGirl : NaturalFloorNihilMonster
{
    public abstract NaturalFloorGirlKind Kind { get; }

    protected abstract NaturalFloorNihilAction[] Rotation { get; }

    protected override IEnumerable<NaturalFloorNihilAction> AvailableActions =>
        Kind == NaturalFloorGirlKind.Love ? Rotation.Append(NaturalFloorNihilAction.LoveMagic).Distinct() : Rotation;

    [SavedProperty]
    public int RotationIndex { get; private set; }

    [SavedProperty]
    public int LoveHitCount { get; private set; }

    [SavedProperty]
    public int PendingGreedBlock { get; private set; }

    [SavedProperty]
    public bool HasReceivedGreedBlock { get; private set; }

    internal override string VisualId => Kind.ToString().ToLowerInvariant();

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new(LibraryResistanceLevel.Endure);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new(LibraryResistanceLevel.Endure);

    protected override IEnumerable<(NaturalFloorNihilAction Action, string Target)> PlanActions()
    {
        if (Kind == NaturalFloorGirlKind.Love && LoveHitCount >= NaturalFloorNihilMoves.LoveHitThreshold)
        {
            yield return (NaturalFloorNihilAction.LoveMagic, "B");
            yield break;
        }

        int index = RotationIndex % Rotation.Length;
        if (Kind == NaturalFloorGirlKind.Love && index == Rotation.Length - 1)
        {
            index = RunRng.MonsterAi.NextInt(Rotation.Length - 1);
        }

        yield return (Rotation[index], "B");
    }

    protected override async Task ApplyPassives()
    {
        await PowerCmdCompat.Ensure<MinionPower>(Creature, 1, Creature, null, silent: true);
        if (Kind == NaturalFloorGirlKind.Love)
        {
            await PowerCmdCompat.Ensure<NaturalFloorLovePower>(Creature, 1, Creature, null, silent: true);
        }
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        await base.AfterDamageGiven(context, dealer, result, props, target, cardSource);
        if (Kind == NaturalFloorGirlKind.Love && dealer == Creature
            && result.UnblockedDamage > 0 && ValuePropCompat.IsPoweredAttack(props))
        {
            LoveHitCount++;
            Creature.GetPower<NaturalFloorLovePower>()?.RefreshCounter();
        }
    }

    protected override async Task AfterAction(NaturalFloorNihilAction action, Dictionary<Creature, int> losses)
    {
        if (action == NaturalFloorNihilAction.LoveMagic)
        {
            LoveHitCount = 0;
            Creature.GetPower<NaturalFloorLovePower>()?.RefreshCounter();
            if (Encounter?.NihilBoss is { Form: NaturalFloorNihilForm.Hatred } boss)
            {
                await boss.QueueForm(NaturalFloorNihilForm.Despair);
            }
        }
        else
        {
            RotationIndex = (RotationIndex + 1) % Rotation.Length;
        }
    }

    internal void QueueGreedBlock()
    {
        if (HasReceivedGreedBlock || PendingGreedBlock > 0)
        {
            return;
        }

        PendingGreedBlock = NaturalFloorNihilMoves.GreedBlockedGirlBlock;
    }

    internal async Task GrantGreedBlock()
    {
        int amount = PendingGreedBlock;
        PendingGreedBlock = 0;
        if (Creature.IsAlive && amount > 0)
        {
            HasReceivedGreedBlock = true;
            await CreatureCmd.GainBlock(Creature, amount, ValueProp.Unpowered, null);
        }
    }
}

public sealed class NaturalFloorLoveGirl : NaturalFloorMagicalGirl
{
    public override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Love;

    public override int MinInitialHp => 70; // 博爱之魔法少女：所有进阶的体力。

    public override int MaxInitialHp => MinInitialHp;

    public override int DefaultChaoResistance => 50; // 博爱之魔法少女：混乱抗性上限。

    protected override NaturalFloorNihilAction[] Rotation =>
    [NaturalFloorNihilAction.LoveMark, NaturalFloorNihilAction.LoveName, NaturalFloorNihilAction.LoveHope, NaturalFloorNihilAction.LoveMark];
}

public sealed class NaturalFloorJusticeGirl : NaturalFloorMagicalGirl
{
    public override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Justice;

    public override int MinInitialHp => 65; // 正义之魔法少女：所有进阶的体力。

    public override int MaxInitialHp => MinInitialHp;

    public override int DefaultChaoResistance => 40; // 正义之魔法少女：混乱抗性上限。

    protected override NaturalFloorNihilAction[] Rotation =>
    [NaturalFloorNihilAction.JusticeHope, NaturalFloorNihilAction.JusticeProtect, NaturalFloorNihilAction.JusticeDefend, NaturalFloorNihilAction.JusticeGuard];
}

public sealed class NaturalFloorHappinessGirl : NaturalFloorMagicalGirl
{
    public override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Happiness;

    public override int MinInitialHp => 80; // 幸福之魔法少女：所有进阶的体力。

    public override int MaxInitialHp => MinInitialHp;

    public override int DefaultChaoResistance => 50; // 幸福之魔法少女：混乱抗性上限。

    protected override NaturalFloorNihilAction[] Rotation =>
    [NaturalFloorNihilAction.HappinessVictory, NaturalFloorNihilAction.HappinessHope, NaturalFloorNihilAction.HappinessGlory, NaturalFloorNihilAction.HappinessGoldenPath];
}

public sealed class NaturalFloorCourageGirl : NaturalFloorMagicalGirl
{
    public override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Courage;

    public override int MinInitialHp => 85; // 勇气之魔法少女：所有进阶的体力。

    public override int MaxInitialHp => MinInitialHp;

    public override int DefaultChaoResistance => 40; // 勇气之魔法少女：混乱抗性上限。

    protected override NaturalFloorNihilAction[] Rotation =>
    [NaturalFloorNihilAction.CourageProtect, NaturalFloorNihilAction.CourageHelp, NaturalFloorNihilAction.CourageJustice, NaturalFloorNihilAction.CourageHope];
}

public abstract class NaturalFloorNihilStatue : NaturalFloorPhaseMonster
{
    private static readonly string[] Ids = ["STATUE"];

    public abstract NaturalFloorGirlKind Kind { get; }

    [SavedProperty]
    public bool SummonPending { get; private set; }

    public override int MinInitialHp => NaturalFloorNihilMoves.StatueHp;

    public override int MaxInitialHp => MinInitialHp;

    public override int DefaultChaoResistance => NaturalFloorNihilMoves.StatueChaos;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new(LibraryResistanceLevel.Normal);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new(LibraryResistanceLevel.Normal);

    protected override string[] MoveIds => Ids;

    internal string VisualId => Kind.ToString().ToLowerInvariant() + "_statue";

    protected override IEnumerable<string> VisualAssets => visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.Assets(VisualId);

    protected override int SelectMove() => 0;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var idle = new MoveState("STATUE", _ => Task.CompletedTask)
        {
            FollowUpStateId = "STATUE"
        };
        return new MonsterMoveStateMachine([idle], idle);
    }

    protected override IEnumerable<MegaCrit.Sts2.Core.MonsterMoves.Intents.AbstractIntent> CreateIntents(int move) => [];

    protected override Task PerformMove(int move, IReadOnlyList<Creature> targets) => Task.CompletedTask;

    protected override async Task ApplyPassives()
    {
        await PowerCmdCompat.Ensure<MinionPower>(Creature, 1, Creature, null, silent: true);
        switch (Kind)
        {
            case NaturalFloorGirlKind.Love:
                await PowerCmdCompat.Ensure<NaturalFloorFrozenHatredPower>(Creature, 1, Creature, null, silent: true);
                break;
            case NaturalFloorGirlKind.Justice:
                await PowerCmdCompat.Ensure<NaturalFloorFrozenDespairPower>(Creature, 1, Creature, null, silent: true);
                break;
            case NaturalFloorGirlKind.Happiness:
                await PowerCmdCompat.Ensure<NaturalFloorFrozenGreedPower>(Creature, 1, Creature, null, silent: true);
                break;
            case NaturalFloorGirlKind.Courage:
                await PowerCmdCompat.Ensure<NaturalFloorFrozenWrathPower>(Creature, 1, Creature, null, silent: true);
                break;
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext context, Creature creature, bool prevented, float deathAnimLength)
    {
        await base.AfterDeath(context, creature, prevented, deathAnimLength);
        if (creature == Creature && !prevented && Encounter?.SettlementTriggered == false)
        {
            SummonPending = true;
            await Encounter.ReplaceBrokenNihilStatues(creature);
        }
    }
}

public sealed class NaturalFloorLoveStatue : NaturalFloorNihilStatue
{
    public override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Love;
}

public sealed class NaturalFloorJusticeStatue : NaturalFloorNihilStatue
{
    public override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Justice;
}

public sealed class NaturalFloorHappinessStatue : NaturalFloorNihilStatue
{
    public override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Happiness;
}

public sealed class NaturalFloorCourageStatue : NaturalFloorNihilStatue
{
    public override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Courage;
}
