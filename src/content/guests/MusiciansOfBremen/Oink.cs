using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.guests.MusiciansOfBremen;

public sealed class Oink : MonsterModel
{
    // Spine 身体的死亡动画由 SpineSpriteDeathAnimPatch 补发；设了时长原版才会等动画播完再溶解
    public override float DeathAnimLengthOverride => AnimationEffects.DeathLength(this, GuestSpine.DeathSeconds);

    public override IEnumerable<string> AssetPaths =>
        MonsterVisualCatalog.GetRequiredProfile(Id.Entry).AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 69, 66);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 71, 68);

    private int HardRehearsalDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12);

    private int TendonChordsDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    private int RedNotesDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private const int RedNotesHits = 2;
    private const int TendonChordsHeal = 10;
    private const int AllyNextTurnStrength = 1;

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

        var hardRehearsal = new MoveState(
            "HARD_REHEARSAL",
            HardRehearsalMove,
            new SingleAttackIntent(HardRehearsalDamage),
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
        random.AddBranch(hardRehearsal, MoveRepeatType.CannotRepeat, 0.3f);
        random.AddBranch(tendonChords, MoveRepeatType.CannotRepeat, 0.2f);
        random.AddBranch(theRedNotes, MoveRepeatType.CannotRepeat, 0.4f);

        hardRehearsal.FollowUpState = random;
        tendonChords.FollowUpState = random;
        theRedNotes.FollowUpState = random;

        states.Add(hardRehearsal);
        states.Add(tendonChords);
        states.Add(theRedNotes);
        states.Add(random);

        return new MonsterMoveStateMachine(states, theRedNotes);
    }

    private async Task HardRehearsalMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(HardRehearsalDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await ApplyNextTurnStrengthToOtherLivingEnemies(AllyNextTurnStrength);
    }

    private async Task TendonChordsMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(TendonChordsDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await HealRandomLivingEnemy(TendonChordsHeal);
        await ApplyNextTurnStrengthToOtherLivingEnemies(AllyNextTurnStrength);
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

    private async Task HealRandomLivingEnemy(decimal amount)
    {
        List<Creature> livingEnemies = CombatState.Enemies
            .Where(enemy => !enemy.IsDead)
            .ToList();
        if (livingEnemies.Count == 0)
        {
            return;
        }

        Creature? target = RunRng.MonsterAi.NextItem(livingEnemies);
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
