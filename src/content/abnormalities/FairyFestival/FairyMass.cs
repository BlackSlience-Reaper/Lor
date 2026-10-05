using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

public enum FairyMassVariant
{
    Left,
    Right
}

public sealed class FairyMass : CounterIntentMonsterModel
{
    // Spine 身体的死亡动画由 SpineSpriteDeathAnimPatch 补发；设了时长原版才会等动画播完再溶解
    public override float DeathAnimLengthOverride => AnimationEffects.DeathLength(this, FairyMassCreatureVisuals.DeathSeconds);

    private const string WingbeatMoveId = "WINGBEAT";
    private const string GluttonyMoveId = "GLUTTONY";

    private const float SegmentDelaySeconds = 1.35f;
    private const int WingbeatHits = 2;
    private const int GluttonyHits = 2; // 贪食：攻击次数，每次攻击后分别治疗。
    private const int GluttonyBaseDamage = 2; // 贪食：低于 DeadlyEnemies 进阶时的单次伤害。
    private const int GluttonyHighAscensionDamage = 3; // 贪食：达到 DeadlyEnemies 进阶时的单次伤害。
    private const int GluttonyHealPerHit = 3;
    private const int WingbeatBleed = 1;

    public const string Root = FairyFestivalAssets.FairyFestivalMonsterRoot;
    public const string IdleTexturePath = Root + "fairy_mass.png";
    public const string IdleAltTexturePath = Root + "fairy_mass_idle_alt.png";
    public const string AttackTexturePath = Root + "fairy_mass_attack.png";
    public const string HitTexturePath = Root + "fairy_mass_hit.png";
    public const string AttackSfxPath = FairyFestivalAssets.MassAttackSfx;

    private FairyMassVariant _variant = FairyMassVariant.Left;

    public override int MinInitialHp => _variant == FairyMassVariant.Left ? 30 : 33;

    public override int MaxInitialHp => _variant == FairyMassVariant.Left ? 34 : 35;

    public override int DefaultChaoResistance => 20;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Vulnerable,
        Pierce = LibraryResistanceLevel.Fatal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Fatal,
        Blunt = LibraryResistanceLevel.Normal
    };

    private static int WingbeatDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private static int GluttonyDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            GluttonyHighAscensionDamage,
            GluttonyBaseDamage);

    public override IEnumerable<string> AssetPaths =>
        FairyMassCreatureVisuals.Profile.AssetPaths
        .Concat(new[] { AttackSfxPath })
        .Concat(EnumerateIntentAssets().SelectMany(intent => intent.AssetPaths))
        .Distinct();

    public void SetVariant(FairyMassVariant variant)
    {
        AssertMutable();
        _variant = variant;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null,silent: true);
        await PowerCmdCompat.Apply<FairyMassCarePower>(Creature, 1m, Creature, null, silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var wingbeat = new MoveState(
            WingbeatMoveId,
            WingbeatMove,
            new BadgedAttackIntent(
                WingbeatDamage,
                WingbeatHits,
                "FAIRY_FESTIVAL_UNBLOCKED_BLEED_ATTACK.description",
                IntentBadge.Bleed(WingbeatBleed)));

        var gluttony = new MoveState(
            GluttonyMoveId,
            GluttonyMove,
            new MultiAttackIntent(GluttonyDamage, GluttonyHits),
            new HealIntent());

        wingbeat.FollowUpState = gluttony;
        gluttony.FollowUpState = wingbeat;

        MonsterState initial = _variant == FairyMassVariant.Left ? wingbeat : gluttony;
        return new MonsterMoveStateMachine(new MonsterState[] { wingbeat, gluttony }, initial);
    }

    private async Task WingbeatMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < WingbeatHits; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            AttackCommand attack = await ExecuteSegmentAttack(WingbeatDamage, i);
            IReadOnlyList<Creature> bleedTargets = AttackCommandCompat.Results(attack)
                .Where(result => result.Receiver.IsPlayer && result.UnblockedDamage > 0)
                .Select(result => result.Receiver)
                .Distinct()
                .ToList();

            if (bleedTargets.Count > 0)
            {
                await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, WingbeatBleed, Creature, null);
            }
        }
    }

    private async Task GluttonyMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < GluttonyHits; i++)
        {
            if (Creature.IsDead)
            {
                return;
            }

            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            await ExecuteSegmentAttack(GluttonyDamage, i);
            await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, GluttonyHealPerHit));
        }
    }

    // 多段攻击只换一次攻击图：第一段等到换图后的命中，后续各段短等待（见 FairyMassCreatureVisuals）
    private Task<AttackCommand> ExecuteSegmentAttack(int damage, int segment)
    {
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(
                "Attack",
                segment == 0 ? FairyMassCreatureVisuals.AttackImpactSeconds : FairyMassCreatureVisuals.FollowUpHitSeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new BadgedAttackIntent(
            WingbeatDamage,
            WingbeatHits,
            "FAIRY_FESTIVAL_UNBLOCKED_BLEED_ATTACK.description",
            IntentBadge.Bleed(WingbeatBleed));
        yield return new MultiAttackIntent(GluttonyDamage, GluttonyHits);
        yield return new HealIntent();
    }
}
