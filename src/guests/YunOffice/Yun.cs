using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.YunOffice;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.guests.YunOffice;

public sealed class Yun : MonsterModel
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 42, 40);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 44, 42);

    public override IEnumerable<string> AssetPaths =>
        YunCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private int YoureTooSlowDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private const int CommandeeringDamage = 4;
    private const int CommandeeringHits = 2;
    private const int CommandeeringGuard = 1;
    private const int CommandeeringGuardTurns = 1;
    private const int YoureTooSlowVulnerable = 1;
    private const int PreparationBlock = 4;
    private const int PreparationNextTurnStrength = 1;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var youreTooSlow = new MoveState(
            "YOU_RE_TOO_SLOW",
            YoureTooSlowMove,
            new SingleAttackIntent(YoureTooSlowDamage),
            new DebuffIntent());

        var commandeering = new MoveState(
            "COMMANDEERING",
            CommandeeringMove,
            new MultiAttackIntent(CommandeeringDamage, CommandeeringHits),
            new BuffIntent());

        var preparation = new MoveState(
            "PREPARATION",
            PreparationMove,
            new DefendIntent(),
            new BuffIntent());

        youreTooSlow.FollowUpState = commandeering;
        commandeering.FollowUpState = preparation;
        preparation.FollowUpState = youreTooSlow;

        states.Add(youreTooSlow);
        states.Add(commandeering);
        states.Add(preparation);

        return new MonsterMoveStateMachine(states, youreTooSlow);
    }

    private async Task YoureTooSlowMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(YoureTooSlowDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await PowerCmdCompat.Apply<FrailPower>(targets, YoureTooSlowVulnerable, Creature, null);
    }

    private async Task CommandeeringMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(CommandeeringDamage)
                .FromMonster(this)
                .WithHitCount(CommandeeringHits)
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await ApplyGuardToLivingEnemies(CommandeeringGuard, CommandeeringGuardTurns);
    }

    private async Task PreparationMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.525f);
        await CreatureCmd.GainBlock(Creature, PreparationBlock, ValueProp.Move, null);
        await ApplyNextTurnStrengthToLivingEnemies(PreparationNextTurnStrength);
    }

    private async Task ApplyGuardToLivingEnemies(decimal amount, int turns)
    {
        IReadOnlyList<Creature> livingEnemies = CombatState.Enemies
            .Where(enemy => !enemy.IsDead)
            .ToList();

        if (livingEnemies.Count > 0)
        {
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                new ThrowingPlayerChoiceContext(),
                livingEnemies,
                amount,
                turns,
                IsPermanent: false,
                Creature,
                null);
        }
    }

    private async Task ApplyNextTurnStrengthToLivingEnemies(decimal amount)
    {
        IReadOnlyList<Creature> livingEnemies = CombatState.Enemies
            .Where(enemy => !enemy.IsDead)
            .ToList();

        if (livingEnemies.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(livingEnemies, amount, Creature, null);
        }
    }
}
