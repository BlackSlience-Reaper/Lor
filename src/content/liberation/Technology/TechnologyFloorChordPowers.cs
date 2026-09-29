using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed class ChordInspiringPower : LibraryOfRuinaPowerModel
{
    private const int GuardAmount = 4;
    private const int HealAmount = 11;

    protected override string LegacyPowerId => "CHORD_INSPIRING_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Guard", GuardAmount),
        new ScaledMonsterHealVar(HealAmount)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != Owner.Side || Owner.IsDead)
        {
            return;
        }

        IReadOnlyList<Creature> allies = combatState.Enemies
            .Where(e => e.IsAlive && e != Owner && e.Side == Owner.Side)
            .ToArray();

        if (allies.Count == 0)
        {
            return;
        }

        Flash();
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(Owner, GuardAmount, 1, Owner, null);

        Creature lowestHpAlly = allies.OrderBy(static a => a.CurrentHp).ThenBy(static a => a.CombatId ?? 0u).First();
        await CreatureCmd.Heal(lowestHpAlly, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(lowestHpAlly, HealAmount));
    }
}

public sealed class ChordEnsemblePower : LibraryOfRuinaPowerModel
{
    private const int ChaosDamagePerIntent = 9;
    private const int StrengthPerHit = 1;
    private const int FlawPerHit = 1;
    private const int FlawTurns = 1;

    protected override string LegacyPowerId => "CHORD_ENSEMBLE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChaosDamage", ChaosDamagePerIntent),
        new PowerVar<StrengthPower>(StrengthPerHit),
        new DynamicVar("Flaw", FlawPerHit),
        new DynamicVar("Turns", FlawTurns)
    ];

    public async Task TriggerEnsemble(int intentCount)
    {
        if (Owner.IsDead || Owner.CombatState is not { } combatState)
        {
            return;
        }

        List<Creature> allies = combatState.Enemies
            .Where(e => e.IsAlive && e != Owner && e.Side == Owner.Side)
            .ToList();

        if (allies.Count == 0)
        {
            return;
        }

        Flash();
        for (int i = 0; i < intentCount; i++)
        {
            Creature? target = combatState.RunState.Rng.MonsterAi.NextItem(allies);
            if (target is not { IsAlive: true })
            {
                continue;
            }

            if (target is LibraryCreature targetLc && targetLc.CurrentChaoValue > 0)
            {
                await LibraryCreatureCmd.ChaoDamage(
                    new ThrowingPlayerChoiceContext(),
                    [targetLc],
                    ChaosDamagePerIntent,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    Owner,
                    null,
                    null);
            }

            await PowerCmdCompat.Apply<StrengthPower>(target, StrengthPerHit, Owner, null);
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(target, FlawPerHit, FlawTurns, Owner, null);
        }
    }
}

public sealed class ChordStaffMelodyCravingPower : LibraryOfRuinaPowerModel
{
    private const int StrengthGain = 1;

    protected override string LegacyPowerId => "CHORD_STAFF_MELODY_CRAVING_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(StrengthGain)
    ];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.IsDead || side != CombatSide.Enemy || Owner.Side != CombatSide.Enemy)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<StrengthPower>(Owner, StrengthGain, Owner, null, silent: false);

        if (Owner.CombatState is { } combatState)
        {
            foreach (Creature enemy in combatState.Enemies)
            {
                if (enemy.IsAlive && enemy.Monster is TechnologyFloorChordBoss chordBoss)
                {
                    chordBoss.OnStaffMelodyCravingTriggered();
                    break;
                }
            }
        }
    }
}
