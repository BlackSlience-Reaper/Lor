using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.HookOffice;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.guests.HookOffice;

public abstract class HookOfficeMonsterBase : MonsterModel
{
    protected const float AttackAnimDelaySeconds = 0.675f;

    internal abstract SpriteVisualProfile SpriteProfile { get; }

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                SpriteProfile.AssetPaths.Count + 12);
            paths.AddRange(SpriteProfile.AssetPaths);

            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

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

    protected async Task<IReadOnlyList<DamageResult>> ExecuteMoveAttack(
        int damage,
        IReadOnlyList<Creature> targets,
        int hitCount = 1)
    {
        if (!CanContinueMove)
        {
            return [];
        }

        using (new TargetedAttackLungeScope(this, targets))
        {
            var attack = DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", AttackAnimDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash");

            if (hitCount > 1)
            {
                attack.WithHitCount(hitCount).OnlyPlayAnimOnce();
            }

            await attack.Execute(null);
            return AttackCommandCompat.Results(attack);
        }
    }

    protected async Task ApplyBleedToUnblockedDamagePlayers(IEnumerable<DamageResult> results, int bleedAmount)
    {
        if (!CanContinueMove)
        {
            return;
        }

        IReadOnlyList<Creature> bleedTargets = results
            .Where(result => result.Receiver.IsPlayer && result.UnblockedDamage > 0)
            .Select(result => result.Receiver)
            .Distinct()
            .ToList();

        if (bleedTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, bleedAmount, Creature, null);
        }
    }

    protected Task GainMoveBlock(int blockAmount)
    {
        return CanContinueMove
            ? CreatureCmd.GainBlock(Creature, blockAmount, ValueProp.Move, null)
            : Task.CompletedTask;
    }

    protected Task GainStrength(int amount)
    {
        return CanContinueMove
            ? PowerCmdCompat.Apply<StrengthPower>(Creature, amount, Creature, null)
            : Task.CompletedTask;
    }

    protected async Task HealSelf(int amount)
    {
        if (!CanContinueMove)
        {
            return;
        }

        await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, amount));
    }

    protected int ScaledHealBadgeAmount(int amount) =>
        (int)MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, amount);

    protected bool CanContinueMove => Creature is { IsDead: false, CombatState: not null };

    protected abstract IEnumerable<AbstractIntent> EnumerateIntentAssets();
}

public sealed class Taein : HookOfficeMonsterBase
{
    private const int GoinFirstBleed = 1;
    private const int GoinFirstBlock = 4;
    private const int RampageHits = 3;
    private const int MutilateDamage = 3;
    private const int MutilateHeal = 3;

    internal override SpriteVisualProfile SpriteProfile =>
        TaeinCreatureVisuals.Profile;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 29, 28);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 32, 31);

    private int GoinFirstFirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private int GoinFirstSecondDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    private int RampageDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var goinFirst = new MoveState(
            "GOIN_FIRST",
            GoinFirstMove,
            new BadgedAttackIntent(
                GoinFirstFirstDamage,
                "HOOK_OFFICE_UNBLOCKED_BLEED_ATTACK.description",
                IntentBadge.Bleed(GoinFirstBleed)),
            new BadgedAttackIntent(
                GoinFirstSecondDamage,
                "HOOK_OFFICE_UNBLOCKED_BLEED_ATTACK.description",
                IntentBadge.Bleed(GoinFirstBleed)),
            new DefendIntent());

        var rampage = new MoveState(
            "RAMPAGE",
            RampageMove,
            new MultiAttackIntent(RampageDamage, RampageHits));

        var mutilate = new MoveState(
            "MUTILATE",
            MutilateMove,
            new BadgedAttackIntent(
                MutilateDamage,
                "HOOK_OFFICE_HEAL_ATTACK.description",
                IntentBadge.Heal(() => ScaledHealBadgeAmount(MutilateHeal))),
            new BadgedAttackIntent(
                MutilateDamage,
                "HOOK_OFFICE_HEAL_ATTACK.description",
                IntentBadge.Heal(() => ScaledHealBadgeAmount(MutilateHeal))));

        goinFirst.FollowUpState = mutilate;
        mutilate.FollowUpState = rampage;
        rampage.FollowUpState = goinFirst;

        states.Add(goinFirst);
        states.Add(mutilate);
        states.Add(rampage);

        return new MonsterMoveStateMachine(states, goinFirst);
    }

    protected override IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new BadgedAttackIntent(
            GoinFirstFirstDamage,
            "HOOK_OFFICE_UNBLOCKED_BLEED_ATTACK.description",
            IntentBadge.Bleed(GoinFirstBleed));
        yield return new BadgedAttackIntent(
            GoinFirstSecondDamage,
            "HOOK_OFFICE_UNBLOCKED_BLEED_ATTACK.description",
            IntentBadge.Bleed(GoinFirstBleed));
        yield return new DefendIntent();
        yield return new MultiAttackIntent(RampageDamage, RampageHits);
        yield return new BadgedAttackIntent(
            MutilateDamage,
            "HOOK_OFFICE_HEAL_ATTACK.description",
            IntentBadge.Heal(() => ScaledHealBadgeAmount(MutilateHeal)));
    }

    private async Task GoinFirstMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> firstResults = await ExecuteMoveAttack(GoinFirstFirstDamage, targets);
        await ApplyBleedToUnblockedDamagePlayers(firstResults, GoinFirstBleed);

        if (!CanContinueMove) return;
        IReadOnlyList<DamageResult> secondResults = await ExecuteMoveAttack(GoinFirstSecondDamage, targets);
        await ApplyBleedToUnblockedDamagePlayers(secondResults, GoinFirstBleed);

        await GainMoveBlock(GoinFirstBlock);
    }

    private Task RampageMove(IReadOnlyList<Creature> targets)
    {
        return ExecuteMoveAttack(RampageDamage, targets, RampageHits);
    }

    private async Task MutilateMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteMoveAttack(MutilateDamage, targets);
        await HealSelf(MutilateHeal);

        if (!CanContinueMove) return;
        await ExecuteMoveAttack(MutilateDamage, targets);
        await HealSelf(MutilateHeal);
    }
}

public sealed class Mccullin : HookOfficeMonsterBase
{
    private const int OverpowerHits = 2;
    private const int PreemptiveStrikeDamage = 5;
    private const int PreemptiveStrikeBlock = 9;
    private const int TrackStrength = 1;
    private const int TrackHeal = 9;

    internal override SpriteVisualProfile SpriteProfile =>
        MccullinCreatureVisuals.Profile;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 31, 30);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 33, 32);

    private int OverpowerDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var overpower = new MoveState(
            "OVERPOWER",
            OverpowerMove,
            new MultiAttackIntent(OverpowerDamage, OverpowerHits));

        var preemptiveStrike = new MoveState(
            "PREEMPTIVE_STRIKE",
            PreemptiveStrikeMove,
            new SingleAttackIntent(PreemptiveStrikeDamage),
            new DefendIntent());

        var track = new MoveState(
            "TRACK",
            TrackMove,
            new BuffIntent(),
            new HealIntent());

        preemptiveStrike.FollowUpState = overpower;
        overpower.FollowUpState = track;
        track.FollowUpState = preemptiveStrike;

        states.Add(overpower);
        states.Add(preemptiveStrike);
        states.Add(track);

        return new MonsterMoveStateMachine(states, preemptiveStrike);
    }

    protected override IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new MultiAttackIntent(OverpowerDamage, OverpowerHits);
        yield return new SingleAttackIntent(PreemptiveStrikeDamage);
        yield return new DefendIntent();
        yield return new BuffIntent();
        yield return new HealIntent();
    }

    private Task OverpowerMove(IReadOnlyList<Creature> targets)
    {
        return ExecuteMoveAttack(OverpowerDamage, targets, OverpowerHits);
    }

    private async Task PreemptiveStrikeMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteMoveAttack(PreemptiveStrikeDamage, targets);
        await GainMoveBlock(PreemptiveStrikeBlock);
    }

    private async Task TrackMove(IReadOnlyList<Creature> targets)
    {
        if (!CanContinueMove)
        {
            return;
        }

        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await GainStrength(TrackStrength);
        await HealSelf(TrackHeal);
    }
}

public sealed class Naoki : HookOfficeMonsterBase
{
    private const int FendFirstDamage = 5;
    private const int FendWeak = 1;
    private const int FendSecondDamage = 4;
    private const int FendBleed = 2;
    private const int QuicknessBlock = 6;
    private const int QuicknessGuard = 2;
    private const int QuicknessGuardTurns = 1;
    private const int MutilateDamage = 2;
    private const int FirstMutilateHeal = 5;
    private const int SecondMutilateHeal = 6;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 20, 19);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 23, 22);

    internal override SpriteVisualProfile SpriteProfile =>
        NaokiCreatureVisuals.Profile;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var fendThisOff = new MoveState(
            "FEND_THIS_OFF_IF_YOU_CAN",
            FendThisOffMove,
            new SingleAttackIntent(FendFirstDamage),
            new DebuffIntent(),
            new BadgedAttackIntent(
                FendSecondDamage,
                "HOOK_OFFICE_UNBLOCKED_BLEED_ATTACK.description",
                IntentBadge.Bleed(FendBleed)));

        var quickness = new MoveState(
            "QUICKNESS",
            QuicknessMove,
            new DefendIntent(),
            new BuffIntent());

        var mutilate = new MoveState(
            "MUTILATE",
            MutilateMove,
            new BadgedAttackIntent(
                MutilateDamage,
                "HOOK_OFFICE_HEAL_ATTACK.description",
                IntentBadge.Heal(() => ScaledHealBadgeAmount(FirstMutilateHeal))),
            new BadgedAttackIntent(
                MutilateDamage,
                "HOOK_OFFICE_HEAL_ATTACK.description",
                IntentBadge.Heal(() => ScaledHealBadgeAmount(SecondMutilateHeal))));

        quickness.FollowUpState = fendThisOff;
        fendThisOff.FollowUpState = mutilate;
        mutilate.FollowUpState = quickness;

        states.Add(fendThisOff);
        states.Add(quickness);
        states.Add(mutilate);

        return new MonsterMoveStateMachine(states, quickness);
    }

    protected override IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new SingleAttackIntent(FendFirstDamage);
        yield return new DebuffIntent();
        yield return new BadgedAttackIntent(
            FendSecondDamage,
            "HOOK_OFFICE_UNBLOCKED_BLEED_ATTACK.description",
            IntentBadge.Bleed(FendBleed));
        yield return new DefendIntent();
        yield return new BuffIntent();
        yield return new BadgedAttackIntent(
            MutilateDamage,
            "HOOK_OFFICE_HEAL_ATTACK.description",
            IntentBadge.Heal(() => ScaledHealBadgeAmount(FirstMutilateHeal)));
        yield return new BadgedAttackIntent(
            MutilateDamage,
            "HOOK_OFFICE_HEAL_ATTACK.description",
            IntentBadge.Heal(() => ScaledHealBadgeAmount(SecondMutilateHeal)));
    }

    private async Task FendThisOffMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteMoveAttack(FendFirstDamage, targets);
        if (!CanContinueMove)
        {
            return;
        }

        await PowerCmdCompat.Apply<WeakPower>(targets, FendWeak, Creature, null);

        IReadOnlyList<DamageResult> results = await ExecuteMoveAttack(FendSecondDamage, targets);
        await ApplyBleedToUnblockedDamagePlayers(results, FendBleed);
    }

    private async Task QuicknessMove(IReadOnlyList<Creature> targets)
    {
        if (!CanContinueMove)
        {
            return;
        }

        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.525f);
        await GainMoveBlock(QuicknessBlock);
        if (!CanContinueMove)
        {
            return;
        }

        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            Creature,
            QuicknessGuard,
            QuicknessGuardTurns,
            Creature,
            null);
    }

    private async Task MutilateMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteMoveAttack(MutilateDamage, targets);
        await HealSelf(FirstMutilateHeal);

        if (!CanContinueMove) return;
        await ExecuteMoveAttack(MutilateDamage, targets);
        await HealSelf(SecondMutilateHeal);
    }
}
