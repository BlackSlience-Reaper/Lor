using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.guests.MusiciansOfBremen;

public sealed class Meow : MonsterModel
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 94, 91);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 96, 94);

    private int TendonChordsDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 13);

    private int RedNotesDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 2, 1);

    private const int RedNotesHits = 7;
    private const int UnforgettableMelodyNextTurnStrength = 2;
    private const int TendonChordsHeal = 10;
    private const int TendonChordsNextTurnStrength = 1;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<LibraryOfRuinaImprovDrummingPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var unforgettableMelody = new MoveState(
            "UNFORGETTABLE_MELODY",
            UnforgettableMelodyMove,
            new BuffIntent());

        var tendonChords = new MoveState(
            "TENDON_CHORDS",
            TendonChordsMove,
            new SingleAttackIntent(TendonChordsDamage),
            new HealIntent(),
            new BuffIntent());

        var theRedNotes = new MoveState(
            "THE_RED_NOTES",
            TheRedNotesMove,
            new MultiAttackIntent(RedNotesDamage, RedNotesHits));

        var random = new RandomBranchState("RAND");
        random.AddBranch(unforgettableMelody, MoveRepeatType.CannotRepeat, 0.2f);
        random.AddBranch(tendonChords, MoveRepeatType.CannotRepeat, 0.5f);
        random.AddBranch(theRedNotes, MoveRepeatType.CannotRepeat, RedNotesWeight);

        unforgettableMelody.FollowUpState = random;
        tendonChords.FollowUpState = random;
        theRedNotes.FollowUpState = random;

        states.Add(unforgettableMelody);
        states.Add(tendonChords);
        states.Add(theRedNotes);
        states.Add(random);

        return new MonsterMoveStateMachine(states, unforgettableMelody);
    }

    private float RedNotesWeight()
    {
        return GetCurrentStrengthAmount() >= 3 ? 0.3f : 0f;
    }

    private int GetCurrentStrengthAmount()
    {
        return Creature.GetPower<StrengthPower>()?.Amount ?? 0;
    }

    private async Task UnforgettableMelodyMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await ApplyNextTurnStrengthToOtherLivingEnemies(UnforgettableMelodyNextTurnStrength);
    }

    private async Task TendonChordsMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(TendonChordsDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await HealRandomOtherLivingEnemy(TendonChordsHeal);
        await ApplyNextTurnStrengthToOtherLivingEnemies(TendonChordsNextTurnStrength);
    }

    private async Task TheRedNotesMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(RedNotesDamage)
            .FromMonster(this)
            .WithHitCount(RedNotesHits)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task HealRandomOtherLivingEnemy(decimal amount)
    {
        List<Creature> otherLivingEnemies = CombatState.Enemies
            .Where(enemy => !enemy.IsDead && enemy != Creature)
            .ToList();
        if (otherLivingEnemies.Count == 0)
        {
            return;
        }

        Creature? target = RunRng.MonsterAi.NextItem(otherLivingEnemies);
        if (target != null)
        {
            await CreatureCmd.Heal(target, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(target, amount));
        }
    }

    private async Task ApplyNextTurnStrengthToOtherLivingEnemies(decimal amount)
    {
        List<Creature> otherLivingEnemies = CombatState.Enemies
            .Where(enemy => !enemy.IsDead && enemy != Creature)
            .ToList();
        if (otherLivingEnemies.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(otherLivingEnemies, amount, Creature, null);
        }
    }
}
