using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.intents;
using LibraryOfRuina.visuals.DawnOffice;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.guests.DawnOffice;

public sealed class Philip : MonsterModel
{
    private static readonly string[] BattleStartLines =
    {
        "PHILIP.dialogue.battleStart.0",
        "PHILIP.dialogue.battleStart.1",
        "PHILIP.dialogue.battleStart.2"
    };

    private static readonly string[] VictoryLines =
    {
        "PHILIP.dialogue.victory.0"
    };

    private static readonly string[] SelfRetreatLines =
    {
        "PHILIP.dialogue.selfRetreat.0",
        "PHILIP.dialogue.selfRetreat.1"
    };

    private static readonly string[] AllyDeathLines =
    {
        "PHILIP.dialogue.allyDeath.0",
        "PHILIP.dialogue.allyDeath.1",
        "PHILIP.dialogue.allyDeath.2"
    };

    private static readonly string[] EnemyDeathLines =
    {
        "PHILIP.dialogue.enemyDeath.0",
        "PHILIP.dialogue.enemyDeath.1",
        "PHILIP.dialogue.enemyDeath.2"
    };

    private const string VictoryOnlySelfLine = "PHILIP.dialogue.victory.onlySelf";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 100, 98);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 103, 101);

    public override IEnumerable<string> AssetPaths =>
        PhilipCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private const int SunsetBladeDamage = 2;
    private const int SunsetBladeHits = 2;
    private const int SunsetBladeBurnPerHit = 1;
    private const int HandleRequestGuardForAllies = 1;
    private const int StigmatizeDamage = 7;
    private const int StigmatizeHits = 2;
    private const int StigmatizeDazedCards = 3;

    private int _openingTurnsRemaining = 3;
    private bool _hasRetreated;
    private bool _hasPlayedBattleStartLine;
    private bool _hasPlayedVictoryLine;
    private int _battleStartLineMask;
    private int _victoryLineMask;
    private int _selfRetreatLineMask;
    private int _allyDeathLineMask;
    private int _enemyDeathLineMask;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _openingTurnsRemaining = 4;
        _hasRetreated = false;
        _hasPlayedBattleStartLine = false;
        _hasPlayedVictoryLine = false;
        _battleStartLineMask = 0;
        _victoryLineMask = 0;
        _selfRetreatLineMask = 0;
        _allyDeathLineMask = 0;
        _enemyDeathLineMask = 0;
        EncounterBgmController.RegisterMonster(Creature);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override Task BeforeCombatStart()
    {
        if (_hasPlayedBattleStartLine || Creature.IsDead)
        {
            return Task.CompletedTask;
        }

        _hasPlayedBattleStartLine = true;
        DawnOfficeDialogueHelper.SpeakNonRepeating(this, BattleStartLines, ref _battleStartLineMask);
        return Task.CompletedTask;
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (_hasPlayedVictoryLine || Creature.IsDead)
        {
            return Task.CompletedTask;
        }

        _hasPlayedVictoryLine = true;
        if (DawnOfficeDialogueHelper.IsOnlyLivingEnemy(this))
        {
            DawnOfficeDialogueHelper.Speak(this, VictoryOnlySelfLine);
        }
        else
        {
            DawnOfficeDialogueHelper.SpeakNonRepeating(this, VictoryLines, ref _victoryLineMask);
        }

        return Task.CompletedTask;
    }

    public override Task BeforeDeath(Creature creature)
    {
        if (creature != Creature || _hasRetreated)
        {
            return Task.CompletedTask;
        }

        DawnOfficeDialogueHelper.SpeakNonRepeating(this, SelfRetreatLines, ref _selfRetreatLineMask);
        return Task.CompletedTask;
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || Creature.IsDead || creature == Creature)
        {
            return Task.CompletedTask;
        }

        if (creature.Side != Creature.Side)
        {
            if (creature.IsPlayer)
            {
                DawnOfficeDialogueHelper.SpeakNonRepeating(this, EnemyDeathLines, ref _enemyDeathLineMask);
            }

            return Task.CompletedTask;
        }

        if (creature.Monster is Salvador or Yuna)
        {
            DawnOfficeDialogueHelper.SpeakNonRepeating(this, AllyDeathLines, ref _allyDeathLineMask);
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var sunsetBlade = new MoveState(
            "SUNSET_BLADE",
            SunsetBladeMove,
            new MultiAttackIntent(SunsetBladeDamage, SunsetBladeHits),
            new DebuffIntent(strong: true));

        var handleRequest = new MoveState(
            "HANDLE_REQUEST",
            HandleRequestMove,
            new BuffIntent());

        var stigmatize = new MoveState(
            "STIGMATIZE",
            StigmatizeMove,
            new MultiAttackIntent(StigmatizeDamage, StigmatizeHits),
            new DetailedStatusCardIntent<Dazed>(
                StigmatizeDazedCards,
                PileType.Draw,
                showSingleTargetMarker: false),
            new EscapeIntent());

        var opening = new RandomBranchState("OPENING");
        opening.AddBranch(sunsetBlade, MoveRepeatType.CannotRepeat, 1f);
        opening.AddBranch(handleRequest, MoveRepeatType.CannotRepeat, 1f);

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(stigmatize, () => _openingTurnsRemaining <= 0);
        chooser.AddState(opening, () => true);

        sunsetBlade.FollowUpState = chooser;
        handleRequest.FollowUpState = chooser;
        stigmatize.FollowUpState = stigmatize;

        states.Add(sunsetBlade);
        states.Add(handleRequest);
        states.Add(stigmatize);
        states.Add(opening);
        states.Add(chooser);

        return new MonsterMoveStateMachine(states, chooser);
    }

    private async Task SunsetBladeMove(IReadOnlyList<Creature> targets)
    {
        ConsumeOpeningTurn();

        var attack = await DamageCmd.Attack(SunsetBladeDamage)
            .FromMonster(this)
            .WithHitCount(SunsetBladeHits)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await ApplyBurnPerHitIgnoringBlock(AttackCommandCompat.Results(attack), SunsetBladeBurnPerHit);
    }

    private async Task HandleRequestMove(IReadOnlyList<Creature> targets)
    {
        ConsumeOpeningTurn();
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await ApplyGuardToOtherLivingEnemies(HandleRequestGuardForAllies);
    }

    private async Task StigmatizeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StigmatizeDamage)
            .FromMonster(this)
            .WithHitCount(StigmatizeHits)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        foreach (Creature target in targets)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(target, PileType.Draw, StigmatizeDazedCards, addedByPlayer: false);
        }

        if (!_hasRetreated)
        {
            _hasRetreated = true;
            DawnOfficeDialogueHelper.SpeakNonRepeating(this, SelfRetreatLines, ref _selfRetreatLineMask);
        }

        await CreatureCmd.Escape(Creature);
    }

    private void ConsumeOpeningTurn()
    {
        if (_openingTurnsRemaining > 0)
        {
            _openingTurnsRemaining--;
        }
    }

    private async Task ApplyBurnPerHitIgnoringBlock(IEnumerable<DamageResult> results, int burnPerHit)
    {
        IReadOnlyList<(Creature target, int hits)> groupedHits = results
            .Where(result => result.Receiver.IsPlayer)
            .GroupBy(result => result.Receiver)
            .Select(group => (target: group.Key, hits: group.Count()))
            .ToList();

        foreach ((Creature target, int hits) in groupedHits)
        {
            await PowerCmdCompat.Apply<LibraryBurnPower>(target, hits * burnPerHit, Creature, null);
        }
    }

    private async Task ApplyGuardToOtherLivingEnemies(decimal amount)
    {
        IReadOnlyList<Creature> allies = CombatState.Enemies
            .Where(enemy => enemy != Creature && !enemy.IsDead)
            .ToList();

        if (allies.Count > 0)
        {
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                new ThrowingPlayerChoiceContext(),
                allies,
                amount,
                1,
                IsPermanent: false,
                Creature,
                null);
        }
    }
}
