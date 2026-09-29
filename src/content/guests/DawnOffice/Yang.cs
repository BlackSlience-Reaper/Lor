using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.guests.DawnOffice;

public sealed class Yang : MonsterModel
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 82, 80);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 84, 83);

    public override IEnumerable<string> AssetPaths =>
        YangCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private int CumulusWallDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    private int SkyClearingCutDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 7);

    private const int CumulusWallBlock = 11;
    private const int SkyClearingCutHits = 2;
    private const int SkyClearingCutBleed = 3;
    private const int CleanUpStrength = 2;
    private const float AttackAnimDelaySeconds = 0.825f;

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

        var cumulusWall = new MoveState(
            "CUMULUS_WALL",
            CumulusWallMove,
            new SingleAttackIntent(CumulusWallDamage),
            new DefendIntent());

        var skyClearingCut = new MoveState(
            "SKY_CLEARING_CUT",
            SkyClearingCutMove,
            new MultiAttackIntent(SkyClearingCutDamage, SkyClearingCutHits),
            new DebuffIntent(strong: true));

        var cleanUp = new MoveState(
            "CLEAN_UP",
            CleanUpMove,
            new BuffIntent());

        cumulusWall.FollowUpState = skyClearingCut;
        skyClearingCut.FollowUpState = cleanUp;
        cleanUp.FollowUpState = cumulusWall;

        states.Add(cumulusWall);
        states.Add(skyClearingCut);
        states.Add(cleanUp);

        return new MonsterMoveStateMachine(states, cumulusWall);
    }

    private async Task CumulusWallMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(CumulusWallDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", AttackAnimDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await CreatureCmd.GainBlock(Creature, CumulusWallBlock, ValueProp.Move, null);
    }

    private async Task SkyClearingCutMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> bleedTargets;
        using (new TargetedAttackLungeScope(this, targets))
        {
            var attack = await DamageCmd.Attack(SkyClearingCutDamage)
                .FromMonster(this)
                .WithHitCount(SkyClearingCutHits)
                .WithAttackerAnim("Attack", AttackAnimDelaySeconds)
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
            await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, SkyClearingCutBleed, Creature, null);
        }
    }

    private async Task CleanUpMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, CleanUpStrength, Creature, null);
    }
}
