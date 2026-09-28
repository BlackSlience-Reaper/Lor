using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.SmilingBodies;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.visuals.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

public sealed class LanguageFloorMeltingCorpse : LorMonsterModel
{
    public const string MoanMoveId = "MOAN";
    public const int MoanHits = 2;
    private const float AttackAnimDelaySeconds = 0.35f;

    public const string Root =
        "res://images/monsters/language_floor_liberation/smiling_face/";
    public const string IdleTexturePath = Root + "melting_corpse_idle.png";
    public const string AttackTexturePath = Root + "melting_corpse_attack.png";

    public static readonly string[] AssetPathsStatic =
        LanguageFloorMeltingCorpseCreatureVisuals.Profile.AssetPaths
            .Concat(
            [
                MeltingCorpse.MoanSfxPath,
                MeltingCorpse.SpawnSfxPath,
                "res://images/powers/language_floor_melting_corpse_rot_power.png"
            ])
            .ToArray();

    public override int MinInitialHp => 40;

    public override int MaxInitialHp => 40;

    public override int DefaultChaoResistance => 100;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Normal
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Resist,
            Pierce = LibraryResistanceLevel.Resist,
            Blunt = LibraryResistanceLevel.Resist
        };

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(new MultiAttackIntent(MoanDamage, MoanHits).AssetPaths)
            .Distinct();

    internal static int MoanDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            2);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Apply<MinionPower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<LanguageFloorMeltingCorpseRotPower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
        LocalOggOneShotPlayer.Play(
            MeltingCorpse.SpawnSfxPath,
            -1.5f);
    }

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);
        if (creature != Creature || creature.IsAlive)
        {
            return;
        }

        LanguageFloorSmilingFace? boss = Creature.CombatState?.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorSmilingFace>()
            .FirstOrDefault();
        if (boss != null)
        {
            await boss.RefreshPlannedTargetsAfterRosterChanged(
                retargetAll: false);
        }
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
        for (int hit = 0; hit < MoanHits && Creature.IsAlive; hit++)
        {
            LocalOggOneShotPlayer.Play(
                MeltingCorpse.MoanSfxPath,
                -2f);
            await DamageCmd.Attack(MoanDamage)
                .FromMonster(this)
                .WithAttackerAnim("Moan", AttackAnimDelaySeconds)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null);
        }
    }
}
