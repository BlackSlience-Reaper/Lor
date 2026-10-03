using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Alriune;

public sealed class Alriune : LorMonsterModel
{
    [SavedProperty]
    public int CycleStep { get; set; }

    [SavedProperty]
    public bool BlossomFirst { get; set; }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        // 克隆后的入场流程重新生成状态机，使招式委托绑定到新实例。
        ResetStateMachine();
    }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            AlriuneNumbers.HighMinHp,
            AlriuneNumbers.MinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            AlriuneNumbers.HighMaxHp,
            AlriuneNumbers.MaxHp);

    public override int DefaultChaoResistance => AlriuneNumbers.MaxChao;

    public override string? StunRecoveryStateId => "ALRIUNE_ROUTER";

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        new(LibraryResistanceLevel.Resist);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Vulnerable,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override IEnumerable<string> AssetPaths => AlriuneAssets.BossAssets
        .Concat(CreateIntents().SelectMany(static intent => intent.AssetPaths))
        .Distinct();

    private static int WinterDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            AlriuneNumbers.WinterHighDamage,
            AlriuneNumbers.WinterDamage);

    private static int BlossomStrength =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            AlriuneNumbers.BlossomHighStrength,
            AlriuneNumbers.BlossomStrength);

    private static int SpringDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            AlriuneNumbers.SpringHighDamage,
            AlriuneNumbers.SpringDamage);

    internal static int CrownTotal(CombatStateLike? combatState)
    {
        int livingPlayers = combatState?.Players.Count(static player => player.Creature.IsAlive) ?? 1;
        return livingPlayers == 0 ? 0 : livingPlayers * AlriuneNumbers.CrownPerLivingPlayer + AlriuneNumbers.CrownExtra;
    }

    private int PreviewCrownTotal => CrownTotal(IsMutable ? CombatState : null);

    private int AutumnHealAmount => (int)Math.Floor(
        (IsMutable ? Creature.MaxHp : MaxInitialHp) * AlriuneNumbers.AutumnHealPercent / 100m);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Ensure<AlriuneSuffocatingAtonementPower>(Creature);
        await PowerCmdCompat.Ensure<AlriuneFlowerTearsPower>(Creature);
    }

    private AbstractIntent[] CreateIntents() =>
    [
        new CombinedAttackDebuffIntent(() => WinterDamage, null, "ALRIUNE_WINTER.description",
            IntentBadge.FromPower<AlriuneAtonementCrownPower>(() => PreviewCrownTotal)),
        new DetailedBuffIntent<StrengthPower>(() => BlossomStrength, DetailedBuffTargetScope.AllEnemies, descriptionKey: "ALRIUNE_BLOSSOM.description"),
        new CombinedDefendBuffIntent(AlriuneNumbers.AutumnBlock, "ALRIUNE_AUTUMN.description"),
        new AlriuneMultiAttackIntent(SpringDamage, AlriuneNumbers.SpringHits, "ALRIUNE_SPRING.description")
    ];

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        AbstractIntent[] intents = CreateIntents();
        MoveState winter = new("WINTER", Winter, intents[0]);
        MoveState blossom = new("BLOSSOM", Blossom, intents[1]);
        MoveState autumn = new("AUTUMN", Autumn, intents[2]);
        MoveState spring = new("SPRING", Spring, intents[3]);
        ConditionalBranchState chooser = new("ALRIUNE_ROUTER");
        chooser.AddState(winter, () => CycleStep == 0);
        chooser.AddState(blossom, () => CycleStep == (BlossomFirst ? 1 : 2));
        chooser.AddState(autumn, () => CycleStep == (BlossomFirst ? 2 : 1));
        chooser.AddState(spring, () => true);
        winter.FollowUpState = chooser;
        blossom.FollowUpState = chooser;
        autumn.FollowUpState = chooser;
        spring.FollowUpState = chooser;
        return new MonsterMoveStateMachine([winter, blossom, autumn, spring, chooser], chooser);
    }

    private async Task Winter(IReadOnlyList<Creature> targets)
    {
        await AttackPlayers(WinterDamage);
        if (Creature.IsAlive)
        {
            await DistributeCrowns(new ThrowingPlayerChoiceContext());
            BlossomFirst = RunRng.MonsterAi.NextInt(2) == 0;
            CycleStep = 1;
        }
    }

    private async Task Blossom(IReadOnlyList<Creature> targets)
    {
        await Guard();
        Creature[] enemies = CombatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .ToArray();
        await PowerCmdCompat.Apply<StrengthPower>(enemies, BlossomStrength, Creature, null);
        CycleStep++;
    }

    private async Task Autumn(IReadOnlyList<Creature> targets)
    {
        await Guard();
        await CreatureCmd.GainBlock(Creature, AlriuneNumbers.AutumnBlock, ValueProp.Move, null);
        await CreatureCmd.Heal(Creature, AutumnHealAmount);
        CycleStep++;
    }

    private async Task Spring(IReadOnlyList<Creature> targets)
    {
        for (int hit = 0; hit < AlriuneNumbers.SpringHits && Creature.IsAlive; hit++)
        {
            await AttackPlayers(SpringDamage);
        }
        CycleStep = 0;
    }

    private async Task AttackPlayers(int damage)
    {
        LocalOggOneShotPlayer.Play(AlriuneAssets.RangedSfx, AlriuneNumbers.SfxVolumeDb);
        await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", AlriuneNumbers.PoseSeconds)
            .Execute(null);
    }

    private Task Guard()
    {
        LocalOggOneShotPlayer.Play(AlriuneAssets.GuardSfx, AlriuneNumbers.SfxVolumeDb);
        return CreatureCmd.TriggerAnim(Creature, "Guard", AlriuneNumbers.PoseSeconds);
    }

    internal async Task DistributeCrowns(PlayerChoiceContext context)
    {
        Creature[] players = CombatState.Players
            .Where(static player => player.Creature.IsAlive)
            .OrderBy(static player => player.NetId)
            .Select(static player => player.Creature)
            .ToArray();
        if (players.Length == 0)
        {
            return;
        }

        int[] amounts = new int[players.Length];
        int total = CrownTotal(CombatState);
        for (int layer = 0; layer < total; layer++)
        {
            amounts[RunRng.MonsterAi.NextInt(players.Length)]++;
        }
        for (int index = 0; index < players.Length; index++)
        {
            if (amounts[index] > 0)
            {
                await PowerCmdCompat.Apply<AlriuneAtonementCrownPower>(context, players[index], amounts[index], Creature, null);
            }
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Creature || wasRemovalPrevented)
        {
            return;
        }
        foreach (Creature minion in CombatState.Enemies
                     .Where(static enemy => enemy.Monster is AlriuneDustborn).ToArray())
        {
            if (minion.GetPower<AlriuneDustToDustPower>() is { } revival)
            {
                await PowerCmd.Remove(revival);
            }
            await CreatureCmd.Kill(minion, force: true);
        }
    }
}
