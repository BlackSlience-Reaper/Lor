using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.FairyFestival;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.History;

public sealed class HistoryFloorPhaseBoss : MonsterModel, ILiberationPrimaryPhaseBoss
{
    private const string AttackMoveId = "ATTACK";
    private const string GuardMoveId = "GUARD";
    private const string SpecialMoveId = "SPECIAL";
    private const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";

    private int _phase = 1;
    private int _turnIndex;
    private MoveState? _reviveAndEmpowerState;

    public override LocString Title => L10NMonsterLookup($"HISTORY_FLOOR_PHASE_BOSS.phase{_phase}.name");

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp => _phase switch
    {
        1 => 62,
        2 => 78,
        3 => 96,
        4 => 120,
        _ => 150
    };

    public override int MaxInitialHp => MinInitialHp;

    public int Phase => _phase;

    public int LiberationPhase => _phase;

    public override IEnumerable<string> AssetPaths =>
        HistoryFloorPhaseBossCreatureVisuals.Profile.AssetPaths
            .Concat(EnumerateIntentAssets().SelectMany(static intent =>
                intent.AssetPaths))
            .Distinct();

    public static string IdleTexturePathForPhase(int phase) => Math.Clamp(phase, 1, HistoryFloorLiberationEncounter.MaxPhase) switch
    {
        1 => HistoryFloorAssets.ScorchedGirlMonsterTexture,
        2 => HistoryFloorAssets.HappyTeddyTexture,
        3 => HistoryFloorAssets.AllAroundHelperTexture,
        4 => FairyQueen.IdleTexturePath,
        _ => HistoryFloorAssets.LeftShoeTexture
    };

    public static string AttackTexturePathForPhase(int phase) => Math.Clamp(phase, 1, HistoryFloorLiberationEncounter.MaxPhase) switch
    {
        1 => HistoryFloorAssets.ScorchedGirlMonsterAttackTexture,
        2 => HistoryFloorAssets.HappyTeddyAttack1Texture,
        3 => HistoryFloorAssets.AllAroundHelperAttackTexture,
        4 => FairyQueen.AttackTexturePath,
        _ => HistoryFloorAssets.LeftShoeAttackTexture
    };

    public static string CastTexturePathForPhase(int phase) => Math.Clamp(phase, 1, HistoryFloorLiberationEncounter.MaxPhase) switch
    {
        2 => HistoryFloorAssets.HappyTeddyAttack2Texture,
        4 => FairyQueen.CastTexturePath,
        5 => HistoryFloorAssets.LeftShoeParryTexture,
        _ => AttackTexturePathForPhase(phase)
    };

    public static string HitTexturePathForPhase(int phase) => Math.Clamp(phase, 1, HistoryFloorLiberationEncounter.MaxPhase) switch
    {
        1 => HistoryFloorAssets.ScorchedGirlMonsterHitTexture,
        2 => HistoryFloorAssets.HappyTeddyHitTexture,
        3 => HistoryFloorAssets.AllAroundHelperHitTexture,
        4 => FairyQueen.HitTexturePath,
        _ => HistoryFloorAssets.LeftShoeHitTexture
    };

    public void ConfigurePhase(int phase)
    {
        AssertMutable();
        _phase = Math.Clamp(phase, 1, HistoryFloorLiberationEncounter.MaxPhase);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _turnIndex = 0;

        if (CombatState?.Encounter is HistoryFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(_phase);
        }

        HistoryFloorLiberationBackgroundController.SetPhaseBackground(_phase);
        EncounterBgmController.RegisterMonster(Creature);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature
            || Creature.CombatState?.Encounter is not HistoryFloorLiberationEncounter encounter)
        {
            return Task.CompletedTask;
        }

        return encounter.OnPhaseBossDeath(this, wasRemovalPrevented, deathAnimLength);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _reviveAndEmpowerState = LiberationPhaseBossMoves.CreateState(ReviveAndEmpowerMoveId, ReviveAndEmpowerMove);

        var attack = new MoveState(
            AttackMoveId,
            AttackMove,
            new SingleAttackIntent(AttackDamage));

        var guard = new MoveState(
            GuardMoveId,
            GuardMove,
            new DefendIntent(),
            new BuffIntent());

        var special = new MoveState(
            SpecialMoveId,
            SpecialMove,
            new MultiAttackIntent(SpecialDamage, SpecialHits),
            new DebuffIntent(strong: _phase >= 4));

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(special, () => _turnIndex % 3 == 2);
        chooser.AddState(guard, () => _turnIndex % 3 == 1);
        chooser.AddState(attack, () => true);

        attack.FollowUpState = chooser;
        guard.FollowUpState = chooser;
        special.FollowUpState = chooser;
        _reviveAndEmpowerState.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[] { _reviveAndEmpowerState, attack, guard, special, chooser },
            chooser);
    }

    private int AttackDamage => _phase switch
    {
        1 => 7,
        2 => 9,
        3 => 11,
        4 => 13,
        _ => 15
    };

    private int SpecialDamage => _phase switch
    {
        1 => 4,
        2 => 5,
        3 => 6,
        4 => 7,
        _ => 8
    };

    private int SpecialHits => _phase >= 4 ? 3 : 2;

    private int GuardBlock => _phase switch
    {
        1 => 8,
        2 => 11,
        3 => 14,
        4 => 17,
        _ => 21
    };

    private async Task AttackMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(AttackDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        AdvanceTurn();
    }

    private async Task GuardMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);
        await CreatureCmd.GainBlock(Creature, GuardBlock, ValueProp.Move, null);
        AdvanceTurn();
    }

    private async Task SpecialMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < SpecialHits; i++)
        {
            if (Creature.IsDead) return;
            await DamageCmd.Attack(SpecialDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        if (_phase >= 3)
        {
            await PowerCmdCompat.Apply<VulnerablePower>(
                targets.Where(static target => target.IsAlive),
                1m,
                Creature,
                null);
        }

        AdvanceTurn();
    }

    public async Task TriggerReviveAndEmpowerState()
    {
        await CreatureCmd.TriggerAnim(Creature, "Hit", 0f);
        ForceReviveAndEmpowerState();
    }

    public void ForceReviveAndEmpowerState()
    {
        LiberationPhaseBossMoves.ForceState(this, _reviveAndEmpowerState);
    }

    private async Task ReviveAndEmpowerMove(IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);
        
        await Cmd.CustomScaledWait(0.3f, 0.6f);

        if (Creature.CombatState?.Encounter is HistoryFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseTransition(this);
        }
    }

    private void AdvanceTurn()
    {
        _turnIndex++;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new SingleAttackIntent(AttackDamage);
        yield return new DefendIntent();
        yield return new BuffIntent();
        yield return new MultiAttackIntent(SpecialDamage, SpecialHits);
        yield return new DebuffIntent(strong: _phase >= 4);
        yield return new HealIntent();
        yield return new BuffIntent();
    }
}
