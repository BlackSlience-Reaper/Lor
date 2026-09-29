using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

/// <summary>
/// 隐士之杖 — 攻击愤怒侍从的辅助小怪。
/// 招式: 咯吱咯吱(多段) / 喀嚓喀嚓(单段+格挡)
/// </summary>
public sealed class HermitStaff : LorMonsterModel, ITargetedMonsterAttackProvider
{
    private const string CreakCreakMoveId = "CREAK_CREAK";
    private const string CrackCrackMoveId = "CRACK_CRACK";

    private const int CreakCreakHits = 2;
    private const int CrackCrackBlock = 9;
    private const float SegmentDelaySeconds = 0.48f;

    private const string Root = "res://images/monsters/hermit_staff/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string AttackTexturePath = Root + "attack.png";
    public const string HitTexturePath = Root + "hit.png";

    private const string SfxRoot = WrathServantEncounterHelper.HermitSfxRoot;

    public static readonly IReadOnlyList<string> AssetPathsStatic =
        HermitStaffCreatureVisuals
            .Profile.AssetPaths;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 94, 67);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 98, 72);

    public override int DefaultChaoResistance => 70;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    private int CreakCreakDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 4);

    private int CrackCrackDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 6);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>();
            paths.AddRange(
                HermitStaffCreatureVisuals
                    .Profile.AssetPaths);

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

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var creakCreak = new MoveState(
            CreakCreakMoveId,
            CreakCreakMove,
            new TargetedMonsterAttackIntent(
                () => CreakCreakDamage,
                () => CreakCreakHits,
                "HERMIT_STAFF_CREAK_CREAK.description"));

        var crackCrack = new MoveState(
            CrackCrackMoveId,
            CrackCrackMove,
            new TargetedMonsterAttackIntent(
                () => CrackCrackDamage,
                () => 1,
                "HERMIT_STAFF_CRACK_CRACK.description"),
            new DefendIntent());

        var rand = new RandomBranchState("RAND");
        rand.AddBranch(creakCreak, MoveRepeatType.CannotRepeat, 1f);
        rand.AddBranch(crackCrack, MoveRepeatType.CannotRepeat, 1f);

        creakCreak.FollowUpState = rand;
        crackCrack.FollowUpState = rand;

        return new MonsterMoveStateMachine([creakCreak, crackCrack, rand], rand);
    }

    private async Task CreakCreakMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < CreakCreakHits; i++)
        {
            if (Creature.IsDead) return;
            await DamageCmd.Attack(CreakCreakDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", SegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null);

            if (i < CreakCreakHits - 1)
            {
                await Cmd.CustomScaledWait(0.04f, 0.08f);
            }
        }
    }

    private async Task CrackCrackMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(CrackCrackDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        await CreatureCmd.GainBlock(Creature, CrackCrackBlock, ValueProp.Move, null);
    }

    // ITargetedMonsterAttackProvider — 始终攻击愤怒侍从
    public bool UsesTargetedAttackContract(Creature owner)
    {
        return true;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        Creature? servant = WrathServantEncounterHelper.FindServant(owner.CombatState);
        return servant != null ? [servant] : [];
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        Creature? servant = WrathServantEncounterHelper.FindServant(owner.CombatState);
        return servant?.Name ?? "Unknown Target";
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new TargetedMonsterAttackIntent(() => CreakCreakDamage, () => CreakCreakHits, "HERMIT_STAFF_CREAK_CREAK.description");
        yield return new TargetedMonsterAttackIntent(() => CrackCrackDamage, () => 1, "HERMIT_STAFF_CRACK_CRACK.description");
        yield return new DefendIntent();
    }
}
