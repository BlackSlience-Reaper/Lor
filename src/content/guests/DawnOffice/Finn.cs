using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.guests.DawnOffice;

public sealed class Finn : MonsterModel
{
    // Spine 身体的死亡动画由 SpineSpriteDeathAnimPatch 补发；设了时长原版才会等动画播完再溶解
    public override float DeathAnimLengthOverride => AnimationEffects.DeathLength(this, GuestSpine.DeathSeconds);

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 65, 63);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 68, 65);

    public override IEnumerable<string> AssetPaths =>
        MonsterVisualCatalog.GetRequiredProfile(Id.Entry).AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private int StruggleDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private int StruggleDodge =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8);

    private int WallopDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);
    
    private const int StruggleGuard = 2;
    private const int WallopHits = 2;
    private const int PreparationBlock = 9;
    private const int PreparationStrength = 1;

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

        var struggle = new MoveState(
            "STRUGGLE",
            StruggleMove,
            new SingleAttackIntent(StruggleDamage),
            new BuffIntent());
            //new DodgeIntent(StruggleDodge));

        var wallop = new MoveState(
            "WALLOP",
            WallopMove,
            new MultiAttackIntent(WallopDamage, WallopHits));

        var preparation = new MoveState(
            "PREPARATION",
            PreparationMove,
            new DefendIntent(),
            new BuffIntent());

        var random = new RandomBranchState("RAND");
        random.AddBranch(struggle, MoveRepeatType.CannotRepeat, 0.3f);
        random.AddBranch(wallop, MoveRepeatType.CannotRepeat, 0.3f);
        random.AddBranch(preparation, MoveRepeatType.CannotRepeat, 0.4f);

        struggle.FollowUpState = random;
        wallop.FollowUpState = random;
        preparation.FollowUpState = random;

        states.Add(struggle);
        states.Add(wallop);
        states.Add(preparation);
        states.Add(random);

        return new MonsterMoveStateMachine(states, random);
    }

    private async Task StruggleMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StruggleDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await LibraryPowerCmd.Apply<LibraryEndurancePower>(Creature, StruggleGuard, 1, Creature, null);
        //await LibraryOfRuinaDodgeDicePower.ApplyDodge(Creature, StruggleDodge, Creature, null);
    }

    private async Task WallopMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(WallopDamage)
            .FromMonster(this)
            .WithHitCount(WallopHits)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task PreparationMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await CreatureCmd.GainBlock(Creature, PreparationBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, PreparationStrength, Creature, null);
    }
}
