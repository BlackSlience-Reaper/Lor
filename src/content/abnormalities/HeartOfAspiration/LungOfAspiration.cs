using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.abnormalities.HeartOfAspiration;

public sealed class LungOfAspiration : AspirationMonsterBase
{
    // Spine 身体的死亡动画由 SpineSpriteDeathAnimPatch 补发；设了时长原版才会等动画播完再溶解
    public override float DeathAnimLengthOverride => LungOfAspirationCreatureVisuals.DeathSeconds;

    private const string ContractingPulseMoveId = "CONTRACTING_PULSE";
    private const string ViolentPulseMoveId = "VIOLENT_PULSE";
    private const int ContractingPulseMinDamage = 12;
    private const int ContractingPulseMaxDamage = 13;
    private const int WeakAmount = 2;
    private const int ViolentPulseMinDamage = 8;
    private const int ViolentPulseMaxDamage = 9;
    private const int ViolentPulseHits = 3;
    private const int StrengthAmount = 3;

    public const string TextureRoot = HeartOfAspirationAssets.LungOfAspirationMonsterRoot;
    public const string IdleTexturePath = TextureRoot + "idle.png";
    public const string AttackTexturePath = TextureRoot + "attack.png";
    public const string HitTexturePath = TextureRoot + "hit.png";
    public const string SpecialTexturePath = TextureRoot + "special.png";
    public const string AttackSfxPath = HeartOfAspirationAssets.LungAttackSfx;

    private static readonly string[] AdditionalAssetPaths =
    [
        AttackSfxPath,
        HeartOfAspirationAssets.LungOfAspirationDesirePassivePowerIcon
    ];

    private int? _contractingPulseDamageRoll;
    private int? _violentPulseDamageRoll;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 306, 270);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 310, 274);

    public override int DefaultChaoResistance => 200;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override IEnumerable<string> AssetPaths =>
        LungOfAspirationCreatureVisuals
            .Profile.AssetPaths
            .Concat(AdditionalAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    protected override Task ApplyAspirationPassive() =>
        PowerCmdCompat.Apply<LungOfAspirationDesirePassivePower>(Creature, 1, Creature, null, silent: true);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState contractingPulse = new(
            ContractingPulseMoveId,
            ContractingPulseMove,
            new SingleAttackIntent(() => GetContractingPulseDamageRoll()),
            new DebuffIntent(),
            new StatusIntent(2));
        MoveState violentPulse = new(
            ViolentPulseMoveId,
            ViolentPulseMove,
            new MultiAttackIntent(ViolentPulseMaxDamage, ViolentPulseHits),
            new BuffIntent());

        contractingPulse.FollowUpState = violentPulse;
        violentPulse.FollowUpState = contractingPulse;

        return new MonsterMoveStateMachine([contractingPulse, violentPulse], contractingPulse);
    }

    private async Task ContractingPulseMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        AttackCommand attack = await DamageCmd.Attack(EnsureContractingPulseDamageRoll())
            .FromMonster(this)
            .WithAttackerAnim("Special", LungOfAspirationCreatureVisuals.AttackImpactSeconds)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        foreach (Creature target in AttackCommandCompat.Results(attack)
            .Where(static result => result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct())
        {
            await PowerCmdCompat.ApplyDebuff<WeakPower>(target, WeakAmount, Creature, null);
        }

        foreach (Creature player in targets)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Toxic>(
                player,
                PileType.Hand,
                1,
                addedByPlayer: false);
            await CardPileCmdCompat.AddToCombatAndPreview<Burn>(
                player,
                PileType.Hand,
                1,
                addedByPlayer: false);
        }

        _contractingPulseDamageRoll = null;
    }

    private async Task ViolentPulseMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await DamageCmd.Attack(EnsureViolentPulseDamageRoll())
            .FromMonster(this)
            .WithHitCount(ViolentPulseHits)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", LungOfAspirationCreatureVisuals.AttackImpactSeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await PowerCmdCompat.Apply<StrengthPower>(Creature, StrengthAmount, Creature, null);
        _violentPulseDamageRoll = null;
    }

    private int GetContractingPulseDamageRoll() =>
        GetDisplayDamageRoll(ref _contractingPulseDamageRoll, ContractingPulseMaxDamage);

    private int EnsureContractingPulseDamageRoll() =>
        EnsureDamageRoll(ref _contractingPulseDamageRoll, ContractingPulseMinDamage, ContractingPulseMaxDamage);

    private int EnsureViolentPulseDamageRoll() =>
        EnsureDamageRoll(ref _violentPulseDamageRoll, ViolentPulseMinDamage, ViolentPulseMaxDamage);

    /// <summary>
    /// Intent display reads the cached roll and never consumes shared RNG.
    /// AttackIntent.GetSingleDamage invokes the damage lambda from local UI
    /// render paths (intent label, hover tips, previews) whose timing and call
    /// counts differ between host and clients; rolling there desynchronizes
    /// the shared MonsterAi stream and the move executes with a different
    /// cached value per peer (observed as StateDivergence after enemy turn).
    /// </summary>
    private int GetDisplayDamageRoll(ref int? cachedRoll, int maxInclusive) =>
        IsMutable ? (cachedRoll ?? maxInclusive) : maxInclusive;

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is not MoveState moveState)
            {
                continue;
            }

            foreach (AbstractIntent intent in moveState.Intents)
            {
                yield return intent;
            }
        }
    }
}
