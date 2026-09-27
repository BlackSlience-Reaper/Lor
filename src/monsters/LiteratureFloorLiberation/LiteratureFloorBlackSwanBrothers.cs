using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.LiteratureFloorLiberation;
using LibraryOfRuina.visuals.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.LiteratureFloorLiberation;

public abstract class LiteratureFloorBlackSwanBrotherBase :
    LibraryMonsterModel
{
    public const string GreenFilthMoveId = "GREEN_FILTH";
    public const string BluffMoveId = "BLUFF";
    public const int GreenFilthHits = 2;
    public const int WeakAmount = 2;
    public const int WeakTurns = 1;
    public const int BluffNextTurnStrength = 1;

    private const string Root =
        "res://images/monsters/literature_floor_liberation/black_swan_brothers/";
    public const string SharedIdleTexturePath = Root + "brother_idle.png";
    public const string SharedAttackTexturePath = Root + "brother_attack.png";
    public const string SharedHitTexturePath = Root + "brother_hit.png";
    public const string SixthAttackTexturePath = Root + "brother_6_attack.png";
    public const string SixthFireTexturePath = Root + "brother_6_fire.png";
    public const string SixthHitTexturePath = Root + "brother_6_hit.png";

    private bool _deathReported;

    public abstract int BrotherNumber { get; }

    public abstract string IdleTexturePath { get; }

    private int GreenFilthDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int BluffDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            8);

    internal static (int Min, int Max) DebugHpRange(bool toughEnemies) =>
        toughEnemies ? (66, 70) : (60, 64);

    internal static int DebugGreenFilthDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 6 : 5;

    internal static int DebugBluffDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 10 : 8;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            106,
            80);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            110,
            84);

    public override int DefaultChaoResistance => 100;

    public override bool ShouldDisappearFromDoom => false;

    public override bool HasDeathSfx => false;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => UniformNormalResistance();

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => UniformNormalResistance();

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                LiteratureFloorBlackSwanBrotherCreatureVisuals.ScenePath,
                IdleTexturePath,
                SharedIdleTexturePath,
                SharedAttackTexturePath,
                SharedHitTexturePath,
                SixthAttackTexturePath,
                SixthFireTexturePath,
                SixthHitTexturePath,
                LiteratureFloorBlackSwanBoss.SlashUpSfxPath,
                LiteratureFloorBlackSwanBoss.SlashDownSfxPath,
                "res://images/powers/library_passive_green.png"
            };
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
        _deathReported = false;
        EncounterBgmController.RegisterMonster(Creature);

        await ApplyBrotherPassive();
        await PowerCmdCompat.Ensure<MinionPower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented
            || creature != Creature
            || _deathReported)
        {
            return;
        }

        _deathReported = true;
        LiteratureFloorBlackSwanBoss? boss = ResolveBlackSwan();
        if (boss != null)
        {
            await boss.OnBrotherDeath(BrotherNumber);
        }
    }

    internal async Task ApplyRoundStartSupport(
        LiteratureFloorBlackSwanBoss boss)
    {
        if (Creature.IsDead || boss.Creature.IsDead)
        {
            return;
        }

        switch (BrotherNumber)
        {
            case 1:
                await PowerCmdCompat.Apply<IntangiblePower>(
                    boss.Creature,
                    1m,
                    Creature,
                    null);
                break;
            case 2:
                await PowerCmdCompat.Apply<StrengthPower>(
                    boss.Creature,
                    1m,
                    Creature,
                    null);
                break;
            case 3:
                await PowerCmdCompat.Apply<PlatingPower>(
                    boss.Creature,
                    LiteratureFloorBlackSwanBoss.ThirdBrotherPlating,
                    Creature,
                    null);
                break;
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var greenFilth = new MoveState(
            GreenFilthMoveId,
            GreenFilthMove,
            new CombinedAttackDebuffIntent(
                () => GreenFilthDamage,
                () => GreenFilthHits,
                "LITERATURE_FLOOR_BLACK_SWAN_BROTHER_GREEN_FILTH.description",
                IntentBadge.FromPower<LibraryWeakPower>(
                    WeakAmount,
                    WeakTurns.ToString(),
                    WeakAmount.ToString())));
        var bluff = new MoveState(
            BluffMoveId,
            BluffMove,
            new CombinedAttackBuffIntent(
                () => BluffDamage,
                () => 1,
                "LITERATURE_FLOOR_BLACK_SWAN_BROTHER_BLUFF.description",
                IntentBadge.NextTurnStrength(BluffNextTurnStrength)));
        var random = new RandomBranchState("BROTHER_RANDOM");
        random.AddBranch(
            greenFilth,
            repeatType: MoveRepeatType.CannotRepeat);
        random.AddBranch(
            bluff,
            repeatType: MoveRepeatType.CannotRepeat);
        greenFilth.FollowUpState = random;
        bluff.FollowUpState = random;

        return new MonsterMoveStateMachine(
            [greenFilth, bluff, random],
            random);
    }

    private async Task GreenFilthMove(IReadOnlyList<Creature> targets)
    {
        string[] triggers = ["Attack", "AttackAlt"];
        foreach (string trigger in triggers)
        {
            if (Creature.IsDead)
            {
                return;
            }

            LocalOggOneShotPlayer.Play(
                trigger == "AttackAlt"
                    ? LiteratureFloorBlackSwanBoss.SlashDownSfxPath
                    : LiteratureFloorBlackSwanBoss.SlashUpSfxPath,
                -2f);
            await DamageCmd.Attack(GreenFilthDamage)
                .FromMonster(this)
                .WithAttackerAnim(
                    trigger,
                    LiteratureFloorBlackSwanBrotherAnimationContract
                        .AttackDurationSeconds)
                .Execute(null);
        }

        foreach (Creature player in LivingPlayers())
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                player,
                WeakAmount,
                WeakTurns,
                Creature,
                null);
        }
    }

    private async Task BluffMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(
            LiteratureFloorBlackSwanBoss.SlashUpSfxPath,
            -2f);
        await DamageCmd.Attack(BluffDamage)
            .FromMonster(this)
            .WithAttackerAnim(
                "Attack",
                LiteratureFloorBlackSwanBrotherAnimationContract
                    .AttackDurationSeconds)
            .Execute(null);

        LiteratureFloorBlackSwanBoss? boss = ResolveBlackSwan();
        if (boss?.Creature is { IsAlive: true } bossCreature)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(
                bossCreature,
                BluffNextTurnStrength,
                Creature,
                null);
        }
    }

    private LiteratureFloorBlackSwanBoss? ResolveBlackSwan() =>
        Creature.CombatState?.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorBlackSwanBoss>()
            .FirstOrDefault();

    private IEnumerable<Creature> LivingPlayers() =>
        Creature.CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray() ?? [];

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new CombinedAttackDebuffIntent(
            () => GreenFilthDamage,
            () => GreenFilthHits,
            "LITERATURE_FLOOR_BLACK_SWAN_BROTHER_GREEN_FILTH.description",
            IntentBadge.FromPower<LibraryWeakPower>(
                WeakAmount,
                WeakTurns.ToString(),
                WeakAmount.ToString()));
        yield return new CombinedAttackBuffIntent(
            () => BluffDamage,
            () => 1,
            "LITERATURE_FLOOR_BLACK_SWAN_BROTHER_BLUFF.description",
            IntentBadge.NextTurnStrength(BluffNextTurnStrength));
    }

    private Task ApplyBrotherPassive() => BrotherNumber switch
    {
        1 => ApplyPassive<LiteratureFloorBlackSwanFirstBrotherPassivePower>(),
        2 => ApplyPassive<LiteratureFloorBlackSwanSecondBrotherPassivePower>(),
        3 => ApplyPassive<LiteratureFloorBlackSwanThirdBrotherPassivePower>(),
        4 => ApplyPassive<LiteratureFloorBlackSwanFourthBrotherPassivePower>(),
        5 => ApplyPassive<LiteratureFloorBlackSwanFifthBrotherPassivePower>(),
        6 => ApplyPassive<LiteratureFloorBlackSwanSixthBrotherPassivePower>(),
        _ => Task.CompletedTask
    };

    private Task ApplyPassive<T>() where T : PowerModel =>
        PowerCmdCompat.Ensure<T>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);

    private static LibraryCreatureResistanceData.Resistance
        UniformNormalResistance() =>
        new(LibraryResistanceLevel.Normal);
}

public sealed class LiteratureFloorBlackSwanFirstBrother :
    LiteratureFloorBlackSwanBrotherBase
{
    public override int BrotherNumber => 1;

    public override string IdleTexturePath =>
        "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_1_idle.png";
}

public sealed class LiteratureFloorBlackSwanSecondBrother :
    LiteratureFloorBlackSwanBrotherBase
{
    public override int BrotherNumber => 2;

    public override string IdleTexturePath =>
        "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_2_idle.png";
}

public sealed class LiteratureFloorBlackSwanThirdBrother :
    LiteratureFloorBlackSwanBrotherBase
{
    public override int BrotherNumber => 3;

    public override string IdleTexturePath =>
        "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_3_idle.png";
}

public sealed class LiteratureFloorBlackSwanFourthBrother :
    LiteratureFloorBlackSwanBrotherBase
{
    public override int BrotherNumber => 4;

    public override string IdleTexturePath =>
        "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_4_idle.png";
}

public sealed class LiteratureFloorBlackSwanFifthBrother :
    LiteratureFloorBlackSwanBrotherBase
{
    public override int BrotherNumber => 5;

    public override string IdleTexturePath =>
        "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_5_idle.png";
}

public sealed class LiteratureFloorBlackSwanSixthBrother :
    LiteratureFloorBlackSwanBrotherBase
{
    public override int BrotherNumber => 6;

    public override string IdleTexturePath =>
        "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_6_idle.png";
}
