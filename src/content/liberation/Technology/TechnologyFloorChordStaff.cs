using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.abnormalities.AddictedEmployee;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Technology;

internal enum TechnologyFloorChordStaffInitialMove
{
    Move1 = 0,
    Move2 = 1,
    Move3 = 2,
    Move4 = 3
}

public sealed class TechnologyFloorChordStaff : LorMonsterModel
{
    // Spine 身体的死亡动画由 SpineSpriteDeathAnimPatch 补发；设了时长原版才会等动画播完再溶解
    public override float DeathAnimLengthOverride => AddictedEmployeeCreatureVisuals.DeathSeconds;

    public override int DefaultChaoResistance => 30;

    private const string ShiveringMoveId = "SHIVERING";
    private const string TremblingStrikeMoveId = "TREMBLING_STRIKE";
    private const string FeelMelodyMoveId = "FEEL_THE_MELODY";
    private const string IWantMoreMoveId = "I_WANT_MORE";

    private const string AttackSfxSlot = "SongMachineAttack";

    private const int ShiveringBlock = 8;
    private const int TremblingStrikeBindAmount = 2;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };
    
    private TechnologyFloorChordStaffInitialMove _initialMove = TechnologyFloorChordStaffInitialMove.Move1;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 55, 54);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 57, 56);

    private int ShiveringDamageA =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private int ShiveringDamageB =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private int TremblingStrikeDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 5);

    private int FeelMelodyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private int IWantMoreDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Skip(1)
            .Concat(
            [
                TechnologyFloorAssets.AddictedEmployeeIdleTexture,
                TechnologyFloorAssets.AddictedEmployeeAttackTexture,
                TechnologyFloorAssets.AddictedEmployeeGuardTexture,
                TechnologyFloorAssets.AddictedEmployeeHitTexture,
                TechnologyFloorAssets.SongMachineAttackSfx
            ])
            .Distinct();

    internal void ConfigurePattern(int startIndex)
    {
        AssertMutable();
        _initialMove = (TechnologyFloorChordStaffInitialMove)(startIndex % 4);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<TechnologyFloorErosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ChordStaffMelodyCravingPower>(Creature, 1m, Creature, null, silent: false);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var shivering = new MoveState(
            ShiveringMoveId,
            ShiveringMove,
            new SingleAttackIntent(ShiveringDamageA),
            new SingleAttackIntent(ShiveringDamageB),
            new DefendIntent());

        var tremblingStrike = new MoveState(
            TremblingStrikeMoveId,
            TremblingStrikeMove,
            new SingleAttackIntent(TremblingStrikeDamage),
            new BadgedDebuffIntent(IntentBadge.Bind(2), amount: 2, descriptionKey: null, strong: true));

        var feelMelody = new MoveState(
            FeelMelodyMoveId,
            FeelTheMelodyMove,
            new MultiAttackIntent(FeelMelodyDamage, 2));

        var iWantMore = new MoveState(
            IWantMoreMoveId,
            IWantMoreMove,
            new SingleAttackIntent(IWantMoreDamage),
            new IWantMoreDebuffIntent());

        shivering.FollowUpState = tremblingStrike;
        tremblingStrike.FollowUpState = feelMelody;
        feelMelody.FollowUpState = iWantMore;
        iWantMore.FollowUpState = shivering;

        List<MonsterState> states = [shivering, tremblingStrike, feelMelody, iWantMore];

        MonsterState initialState = _initialMove switch
        {
            TechnologyFloorChordStaffInitialMove.Move2 => tremblingStrike,
            TechnologyFloorChordStaffInitialMove.Move3 => feelMelody,
            TechnologyFloorChordStaffInitialMove.Move4 => iWantMore,
            _ => shivering
        };

        return new MonsterMoveStateMachine(states, initialState);
    }

    private async Task ShiveringMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> resultsA = await ExecuteAttackSegment(ShiveringDamageA);
        foreach (DamageResult result in resultsA)
        {
            if (result.BlockedDamage > 0 && !result.WasBlockBroken)
            {
                await LibraryPowerCmd.Apply<LibraryDisarmPower>(
                    new ThrowingPlayerChoiceContext(),
                    Creature,
                    amount: 1,
                    0,
                    IsPermanent: false,
                    Creature,
                    null);
                break;
            }
        }

        IReadOnlyList<DamageResult> resultsB = await ExecuteAttackSegment(ShiveringDamageB);
        foreach (DamageResult result in resultsB)
        {
            if (result.BlockedDamage > 0 && !result.WasBlockBroken)
            {
                await PowerCmdCompat.Apply<WeakPower>(Creature, 1m, Creature, null);
                break;
            }
        }

        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.075f);
        await CreatureCmd.GainBlock(Creature, ShiveringBlock, ValueProp.Move, null);
    }

    private async Task TremblingStrikeMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> results = await ExecuteAttackSegment(TremblingStrikeDamage);

        IReadOnlyList<Creature> hitTargets = results
            .Where(static result => result.Receiver.IsAlive && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();

        if (hitTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryBindingPower>(hitTargets, TremblingStrikeBindAmount, Creature, null);
        }
    }

    private async Task FeelTheMelodyMove(IReadOnlyList<Creature> targets)
    {
        bool anyBlockedNotBroken = false;

        for (int hit = 0; hit < 2; hit++)
        {
            IReadOnlyList<DamageResult> results = await ExecuteAttackSegment(FeelMelodyDamage);
            if (!anyBlockedNotBroken)
            {
                foreach (DamageResult result in results)
                {
                    if (result.BlockedDamage > 0 && !result.WasBlockBroken)
                    {
                        anyBlockedNotBroken = true;
                        break;
                    }
                }
            }
        }

        if (anyBlockedNotBroken)
        {
            if (Creature is LibraryCreature lc && lc.CurrentChaoValue > 0)
            {
                await LibraryCreatureCmd.ChaoDamage(
                    new ThrowingPlayerChoiceContext(),
                    [lc],
                    10,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    Creature,
                    null,
                    null);
            }
        }
    }

    private async Task IWantMoreMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> results = await ExecuteAttackSegment(IWantMoreDamage);

        foreach (DamageResult result in results)
        {
            if (result.UnblockedDamage <= 0 || !result.Receiver.IsAlive)
            {
                continue;
            }

            int maxHpLoss = (int)(result.UnblockedDamage * 0.1m * result.Receiver.CurrentHp);
            if (maxHpLoss > 0)
            {
                await CreatureCmdCompat.Damage(new ThrowingPlayerChoiceContext(), result.Receiver, maxHpLoss, ValueProp.Unblockable | ValueProp.Unpowered, Creature, null);
            }
        }
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteAttackSegment(int damage)
    {
        LocalOggOneShotPlayer.PlayExclusive(AttackSfxSlot, TechnologyFloorAssets.SongMachineAttackSfx, -2f);
        AttackCommand attack = await DamageCmd.Attack(damage)
            .FromMonster(this)
            // 等待对准 Spine 攻击动画的命中帧（与原版怪物攻击的默认等待相同），见 AddictedEmployeeCreatureVisuals
            .WithAttackerAnim("Attack", AddictedEmployeeCreatureVisuals.AttackImpactSeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        LocalOggOneShotPlayer.StopExclusive(AttackSfxSlot);
        return AttackCommandCompat.Results(attack);
    }
}
