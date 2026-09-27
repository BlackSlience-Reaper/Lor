using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.DawnOffice;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.guests.DawnOffice;

public sealed class Gin : MonsterModel
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 81, 79);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 83, 81);

    public override IEnumerable<string> AssetPaths =>
        GinCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private int ScatteringSlashDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 13);

    private const int ScatteringSlashGuard = 1;
    private const int SilentMistBlock = 12;
    private const int SilentMistGuard = 3;
    private const int SilentMistNextTurnStrength = 2;
    private const int CleanUpVulnerable = 3;
    private const int CleanUpGuard = 1;
    private const float AttackAnimDelaySeconds = 0.825f;

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

        var scatteringSlash = new MoveState(
            "SCATTERING_SLASH",
            ScatteringSlashMove,
            new SingleAttackIntent(ScatteringSlashDamage),
            new BuffIntent());

        var silentMist = new MoveState(
            "SILENT_MIST",
            SilentMistMove,
            new DefendIntent(),
            new BuffIntent());

        var cleanUp = new MoveState(
            "CLEAN_UP",
            CleanUpMove,
            new DebuffIntent(strong: true),
            new BuffIntent());

        scatteringSlash.FollowUpState = silentMist;
        silentMist.FollowUpState = cleanUp;
        cleanUp.FollowUpState = scatteringSlash;

        states.Add(scatteringSlash);
        states.Add(silentMist);
        states.Add(cleanUp);

        return new MonsterMoveStateMachine(states, scatteringSlash);
    }

    private async Task ScatteringSlashMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(ScatteringSlashDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", AttackAnimDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await ApplyGuardToLivingEnemies(ScatteringSlashGuard);
    }

    private async Task SilentMistMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await CreatureCmd.GainBlock(Creature, SilentMistBlock, ValueProp.Move, null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(Creature, SilentMistGuard, 1, Creature, null);
        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, SilentMistNextTurnStrength, Creature, null);
    }

    private async Task CleanUpMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await PowerCmdCompat.Apply<FrailPower>(targets, CleanUpVulnerable, Creature, null);
        await ApplyGuardToLivingEnemies(CleanUpGuard);
    }

    private async Task ApplyGuardToLivingEnemies(decimal amount)
    {
        IReadOnlyList<Creature> livingEnemies = CombatState.Enemies
            .Where(enemy => !enemy.IsDead)
            .ToList();

        if (livingEnemies.Count > 0)
        {
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                new ThrowingPlayerChoiceContext(),
                livingEnemies,
                amount,
                1,
                IsPermanent: false,
                Creature,
                null);
        }
    }
}
