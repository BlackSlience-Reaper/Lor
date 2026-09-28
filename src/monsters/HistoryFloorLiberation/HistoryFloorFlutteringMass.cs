using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.visuals.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.HistoryFloorLiberation;

public enum HistoryFloorFlutteringMassPattern
{
    GluttonyFirst,
    WingbeatFirst
}

public sealed class HistoryFloorFlutteringMass : LorMonsterModel
{
    private const string GluttonyMoveId = "GLUTTONY";
    private const string WingbeatMoveId = "WINGBEAT";

    private const float SegmentDelaySeconds = 1.25f;

    public override int DefaultChaoResistance => 40;

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Pierce = LibraryResistanceLevel.Vulnerable,
        Blunt = LibraryResistanceLevel.Fatal
    };

    private const int GluttonyHealPerHit = 5;
    private const int WingbeatBlock = 4;
    private const int BleedAmount = 1;

    public const string Root = "res://images/monsters/history_floor/fluttering/";
    public const string IdleTexturePath = Root + "mass_idle.png";
    public const string AttackTexturePath = Root + "mass_attack.png";
    public const string HitTexturePath = Root + "mass_hit.png";
    public const string AttackSfxPath = "res://audio/sfx/history_floor/fluttering/mass_attack.ogg";

    private HistoryFloorFlutteringMassPattern _pattern = HistoryFloorFlutteringMassPattern.GluttonyFirst;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 35, 33);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 36, 34);

    private static int GluttonyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private static int WingbeatDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    public override IEnumerable<string> AssetPaths =>
        HistoryFloorFlutteringMassCreatureVisuals.Profile.AssetPaths
        .Append(AttackSfxPath)
        .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
        .Distinct();

    public void ConfigurePattern(HistoryFloorFlutteringMassPattern pattern)
    {
        AssertMutable();
        _pattern = pattern;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Apply<FlutteringMassCarePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var gluttony = new MoveState(
            GluttonyMoveId,
            GluttonyMove,
            new SingleAttackIntent(GluttonyDamage),
            new SingleAttackIntent(GluttonyDamage),
            new HealIntent());

        var wingbeat = new MoveState(
            WingbeatMoveId,
            WingbeatMove,
            CreateBleedIntent(WingbeatDamage),
            new DefendIntent());

        gluttony.FollowUpState = wingbeat;
        wingbeat.FollowUpState = gluttony;

        MonsterState initial = _pattern == HistoryFloorFlutteringMassPattern.GluttonyFirst ? gluttony : wingbeat;
        return new MonsterMoveStateMachine(new MonsterState[] { gluttony, wingbeat }, initial);
    }

    private async Task GluttonyMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < 2; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            AttackCommand attack = await ExecuteSegmentAttack(GluttonyDamage);
            bool healed = AttackCommandCompat.Results(attack)
                .Any(static result => result.UnblockedDamage > 0);
            if (healed)
            {
                await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, GluttonyHealPerHit));
            }
        }
    }

    private async Task WingbeatMove(IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead) return;
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        AttackCommand attack = await ExecuteSegmentAttack(WingbeatDamage);
        IReadOnlyList<Creature> bleedTargets = AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsPlayer && result.Receiver.IsAlive && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToList();
        if (bleedTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, BleedAmount, Creature, null);
        }

        await CreatureCmd.GainBlock(Creature, WingbeatBlock, ValueProp.Move, null);
    }

    private Task<AttackCommand> ExecuteSegmentAttack(int damage)
    {
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new SingleAttackIntent(GluttonyDamage);
        yield return new HealIntent();
        yield return CreateBleedIntent(WingbeatDamage);
        yield return new DefendIntent();
    }

    private static BadgedAttackIntent CreateBleedIntent(int damage) =>
        new(
            damage,
            "FLUTTERING_UNBLOCKED_BLEED_ATTACK.description",
            IntentBadge.Bleed(BleedAmount));
}
