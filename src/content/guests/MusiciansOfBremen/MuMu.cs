using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.guests.MusiciansOfBremen;

public sealed class MuMu : MonsterModel
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 81, 79);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 85, 82);

    private int ShrineToMusicDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 11);

    private int RedNotesDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    private const int RedNotesHits = 2;
    private const int HeavyPeaksWeak = 1;
    private const int ShrineToMusicDazed = 2;

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

        var heavyPeaks = new MoveState(
            "HEAVY_PEAKS",
            HeavyPeaksMove,
            new DebuffIntent());

        var shrineToMusic = new MoveState(
            "SHRINE_TO_MUSIC",
            ShrineToMusicMove,
            new SingleAttackIntent(ShrineToMusicDamage),
            new DetailedStatusCardIntent<Dazed>(
                ShrineToMusicDazed,
                PileType.Discard,
                showSingleTargetMarker: false));

        var theRedNotes = new MoveState(
            "THE_RED_NOTES",
            TheRedNotesMove,
            new MultiAttackIntent(RedNotesDamage, RedNotesHits));

        var opening = new RandomBranchState("OPENING");
        opening.AddBranch(heavyPeaks, MoveRepeatType.CanRepeatForever, 1f);
        opening.AddBranch(shrineToMusic, MoveRepeatType.CanRepeatForever, 1f);
        opening.AddBranch(theRedNotes, MoveRepeatType.CanRepeatForever, 1f);

        heavyPeaks.FollowUpState = shrineToMusic;
        shrineToMusic.FollowUpState = theRedNotes;
        theRedNotes.FollowUpState = heavyPeaks;

        states.Add(heavyPeaks);
        states.Add(shrineToMusic);
        states.Add(theRedNotes);
        states.Add(opening);

        return new MonsterMoveStateMachine(states, opening);
    }

    private async Task HeavyPeaksMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await PowerCmdCompat.Apply<WeakPower>(targets, HeavyPeaksWeak, Creature, null);
    }

    private async Task ShrineToMusicMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ShrineToMusicDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        foreach (Creature target in targets)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(
                target,
                PileType.Discard,
                ShrineToMusicDazed,
                addedByPlayer: false);
        }
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
}
