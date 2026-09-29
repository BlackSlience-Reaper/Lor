using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.DawnOffice;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.DawnOffice;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.guests.DawnOffice;
public sealed class Sayo : MonsterModel
{
    private static readonly string[] AttackAnimTriggers =
    {
        "AttackStrike",
        "AttackThrust",
        "AttackSlash",
    };
    private int _attackAnimCursor = -1;

    public override int MinInitialHp => 
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 88, 84);

    public override int MaxInitialHp => 
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 90, 86);

    public override IEnumerable<string> AssetPaths =>
        SayoCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private int SharpenedBladeDamage => 
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 7);

    private int SharpenedBladeBlock => 
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    private int BackStreetsDamage => 
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private const int CleanUpDazedAmount = 4;
    private const int CleanUpWeakAmount = 2;
    private const int BackStreetsBleed = 3;
    private const int BackStreetsMultiHits = 2;
    private const float AttackAnimDelaySeconds = 0.825f;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<LibraryOfRuinaInkOverPower>(
            Creature, 1, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();
        var SharpenedBladeState = new MoveState(
            "SHARPENED_BLADE_BLOCK",
            SharpenedBladeMove,
            new SingleAttackIntent(SharpenedBladeDamage), new DefendIntent()
        );
        var BackStreetsState = new MoveState(
            "BACK_STREETS",
            BackStreetsMove,
            new MultiAttackIntent(BackStreetsDamage, BackStreetsMultiHits),
            new DebuffIntent(true)
        );
        var CleanUpState = new MoveState(
            "CLEAN_UP",
            CleanUpMove,
            new DetailedStatusCardIntent<Dazed>(
                CleanUpDazedAmount,
                PileType.Discard,
                showSingleTargetMarker: false),
            new DebuffIntent()
        );
        BackStreetsState.FollowUpState = SharpenedBladeState;
        CleanUpState.FollowUpState = BackStreetsState;
        SharpenedBladeState.FollowUpState = CleanUpState;

        states.Add(SharpenedBladeState);
        states.Add(BackStreetsState);
        states.Add(CleanUpState);

        return new MonsterMoveStateMachine(states, BackStreetsState);
    }

    private async Task SharpenedBladeMove(IReadOnlyList<Creature> targets)
    {
        string attackTrigger = GetRandomAttackTrigger();

        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(SharpenedBladeDamage)
                .FromMonster(this)
                .WithAttackerAnim(attackTrigger, AttackAnimDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }
    }

    private async Task BackStreetsMove(IReadOnlyList<Creature> targets)
    {
        string attackTrigger = GetRandomAttackTrigger();
        IReadOnlyList<Creature> bleedTargets;
        using (new TargetedAttackLungeScope(this, targets))
        {
            var attack = await DamageCmd.Attack(BackStreetsDamage)
                .FromMonster(this)
                .WithHitCount(BackStreetsMultiHits)
                .OnlyPlayAnimOnce()
                .WithAttackerAnim(attackTrigger, AttackAnimDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);

            bleedTargets = AttackCommandCompat.Results(attack)
                .Where(result => result.Receiver.IsPlayer && result.UnblockedDamage > 0)
                .Select(result => result.Receiver)
                .Distinct()
                .ToList();
        }

        if (bleedTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, BackStreetsBleed, Creature, null);
        }
    }

    private async Task CleanUpMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        foreach (Creature t in targets)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(t, PileType.Discard, CleanUpDazedAmount, addedByPlayer: false);
        }
        await PowerCmdCompat.Apply<WeakPower>(targets, CleanUpWeakAmount, Creature, null);
    }

    private string GetRandomAttackTrigger()
    {
        if (AttackAnimTriggers.Length == 0)
        {
            return "AttackStrike";
        }

        _attackAnimCursor = (_attackAnimCursor + 1) % AttackAnimTriggers.Length;
        return AttackAnimTriggers[_attackAnimCursor];
    }
    
}
