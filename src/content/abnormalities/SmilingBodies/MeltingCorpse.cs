using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.abnormalities.SmilingBodies;

public sealed class MeltingCorpse : LorMonsterModel
{
    public override float DeathAnimLengthOverride => LayeredBossSpine.DeathLength(this);

    private const string MoanMoveId = "MOAN";
    private const int MoanBaseDamage = 1;
    private const int MoanHighAscensionDamage = 2;
    private const int MoanHits = 2;
    private const float AttackAnimDelaySeconds = 0.35f;

    public const string Root = SmilingBodiesAssets.SmilingBodiesMonsterRoot;
    public const string IdleTexturePath = Root + "melting_corpse_idle.png";

    public const string SfxRoot = SmilingBodiesAssets.SmilingBodiesSfxRoot;
    public const string MoanSfxPath = SfxRoot + "melting_corpse_moan.ogg";
    public const string SpawnSfxPath = SfxRoot + "corpse_spawn.ogg";

    public static readonly string[] AssetPathsStatic =
        MeltingCorpseCreatureVisuals
            .Profile.AssetPaths
            .Concat([MoanSfxPath, SpawnSfxPath])
            .ToArray();

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 50, 46);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 50, 50);

    public override int DefaultChaoResistance => 30;

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

    private static int MoanDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, MoanHighAscensionDamage, MoanBaseDamage);

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ThornsPower>(Creature, 6m, Creature, null, silent: true);
        LocalOggOneShotPlayer.Play(SpawnSfxPath, -1.5f);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var moan = new MoveState(
            MoanMoveId,
            MoanMove,
            new MultiAttackIntent(MoanDamage, MoanHits));

        moan.FollowUpState = moan;
        return new MonsterMoveStateMachine([moan], moan);
    }

    private async Task MoanMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < MoanHits; i++)
        {
            if (Creature.IsDead)
            {
                return;
            }

            LocalOggOneShotPlayer.Play(MoanSfxPath, -2f);
            await DamageCmd.Attack(MoanDamage)
                .FromMonster(this)
                .WithAttackerAnim("Moan", AttackAnimDelaySeconds)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null);
        }
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new MultiAttackIntent(MoanDamage, MoanHits);
    }
}
