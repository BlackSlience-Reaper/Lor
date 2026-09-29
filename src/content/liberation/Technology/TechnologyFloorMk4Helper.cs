using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.AllAroundHelper;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Technology;

internal enum TechnologyFloorMk4HelperInitialMove
{
    Charge,
    Clean,
    Rest
}

public sealed class TechnologyFloorMk4Helper : LorMonsterModel
{
    public override int DefaultChaoResistance => 30;

    private const int NormalMinHp = 38;
    private const int NormalMaxHp = 40;
    private const int HighAscensionMinHp = 41;
    private const int HighAscensionMaxHp = 44;

    private const int ChargeBlock = 12;
    private const int ChargeStrength = 1;
    private const int ChargeGuardAmount = 1;
    private const int ChargeGuardTurns = 1;

    private const int CleanBaseDamage = 9;
    private const int CleanHighAscensionDamage = 10;

    private const int RestDazedCount = 2;

    private const string ChargeMoveId = "CHARGE";
    private const string CleanMoveId = "CLEAN";
    private const string RestMoveId = "REST";

    private const string AttackSfxPath = "res://audio/sfx/all_around_helper/all_around_helper_attack.ogg";
    public const string IdleTexturePath = "res://images/monsters/all_around_helper.webp";
    public const string AttackTexturePath = "res://images/monsters/all_around_helper_attack.webp";
    public const string HitTexturePath = "res://images/monsters/all_around_helper_hit.webp";

    private TechnologyFloorMk4HelperInitialMove _initialMove = TechnologyFloorMk4HelperInitialMove.Charge;

    private int CleanDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, CleanHighAscensionDamage, CleanBaseDamage);

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMinHp, NormalMinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMaxHp, NormalMaxHp);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                TechnologyFloorMk4HelperCreatureVisuals.Profile.AssetPaths.Count
                + 8);
            paths.AddRange(
                TechnologyFloorMk4HelperCreatureVisuals.Profile.AssetPaths);
            paths.Add(AttackSfxPath);

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

        await PowerCmdCompat.Apply<LibraryOfRuinaAllAroundHelperRecognitionModePower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);

        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
    }

    internal void ConfigureInitialMove(TechnologyFloorMk4HelperInitialMove initialMove)
    {
        AssertMutable();
        _initialMove = initialMove;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var charge = new MoveState(
            ChargeMoveId,
            ChargeMove,
            new DefendIntent(),
            new BuffIntent());

        var clean = new MoveState(
            CleanMoveId,
            CleanMove,
            new SingleAttackIntent(CleanDamage));

        var rest = new MoveState(
            RestMoveId,
            RestMove,
            new DetailedStatusCardIntent<Dazed>(
                RestDazedCount,
                PileType.Discard,
                showSingleTargetMarker: false));

        charge.FollowUpState = clean;
        clean.FollowUpState = rest;
        rest.FollowUpState = charge;

        List<MonsterState> states =
        [
            charge,
            clean,
            rest
        ];

        MonsterState initialState = _initialMove switch
        {
            TechnologyFloorMk4HelperInitialMove.Clean => clean,
            TechnologyFloorMk4HelperInitialMove.Rest => rest,
            _ => charge
        };

        return new MonsterMoveStateMachine(states, initialState);
    }

    private async Task ChargeMove(IReadOnlyList<Creature> targets)
    {
        await AbnormalityAnimHelper.TriggerCast(Creature);
        await CreatureCmd.GainBlock(Creature, ChargeBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, ChargeStrength, Creature, null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            Creature,
            ChargeGuardAmount,
            ChargeGuardTurns,
            Creature,
            null);
    }

    private async Task CleanMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await DamageCmd.Attack(CleanDamage)
            .FromMonster(this)
            .WithAttackerAnim(
                "Attack",
                AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .SpawningHitVfxOnEachCreature()
            .Execute(null);
    }

    private async Task RestMove(IReadOnlyList<Creature> targets)
    {
        await AbnormalityAnimHelper.TriggerCast(Creature);

        var playerCreatures = targets.Where(c => c is { IsDead: false, IsPlayer: true }).ToList();

        if (playerCreatures.Count > 0)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(
                playerCreatures,
                PileType.Discard,
                RestDazedCount,
                addedByPlayer: false);
        }
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (!state.IsMove || state is not MoveState moveState)
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
