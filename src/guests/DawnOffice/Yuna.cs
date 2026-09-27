using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.powers;
using LibraryOfRuina.visuals.DawnOffice;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.guests.DawnOffice;

public sealed class Yuna : MonsterModel
{
    private static readonly string[] BattleStartLines =
    {
        "YUNA.dialogue.battleStart.0",
        "YUNA.dialogue.battleStart.1",
        "YUNA.dialogue.battleStart.2"
    };

    private static readonly string[] VictoryLines =
    {
        "YUNA.dialogue.victory.0"
    };

    private static readonly string[] EnemyDeathLines =
    {
        "YUNA.dialogue.enemyDeath.0",
        "YUNA.dialogue.enemyDeath.1",
        "YUNA.dialogue.enemyDeath.2"
    };

    private const string VictoryOnlySelfLine = "YUNA.dialogue.victory.onlySelf";
    private const string SelfDeathDefaultLine = "YUNA.dialogue.selfDeath.default";
    private const string SelfDeathAfterSalvadorDeathLine = "YUNA.dialogue.selfDeath.afterSalvadorDeath";
    private const string AllyDeathSalvadorBeforePhilipRetreatLine = "YUNA.dialogue.allyDeath.salvadorBeforePhilipRetreat";
    private const string AllyDeathSalvadorAfterPhilipRetreatLine = "YUNA.dialogue.allyDeath.salvadorAfterPhilipRetreat";
    private const string AllyDeathPhilipRetreatLine = "YUNA.dialogue.allyDeath.philipRetreat";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 70, 68);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 73, 71);

    public override IEnumerable<string> AssetPaths =>
        YunaCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private const int ButterflySlashDamage = 5;
    private const int ButterflySlashHits = 2;
    private const int ButterflySlashBurnPerHit = 2;
    private const int EjectCardsPerTurn = 2;
    private const int HandleRequestBlock = 12;
    private const int HandleRequestGuardForAllies = 1;
    private bool _isSubscribedToCreatureChanges;
    private bool _hasObservedPhilipRetreat;
    private bool _hasPlayedBattleStartLine;
    private bool _hasPlayedVictoryLine;
    private int _battleStartLineMask;
    private int _victoryLineMask;
    private int _enemyDeathLineMask;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _isSubscribedToCreatureChanges = false;
        _hasObservedPhilipRetreat = false;
        _hasPlayedBattleStartLine = false;
        _hasPlayedVictoryLine = false;
        _battleStartLineMask = 0;
        _victoryLineMask = 0;
        _enemyDeathLineMask = 0;

        EncounterBgmController.RegisterMonster(Creature);
        SubscribeToCreatureChanges();
        _hasObservedPhilipRetreat = DawnOfficeDialogueHelper.HasEscapedTeammate<Philip>(this);
    }

    public override void BeforeRemovedFromRoom()
    {
        UnsubscribeFromCreatureChanges();
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
        if (creature != Creature)
        {
            return Task.CompletedTask;
        }

        if (DawnOfficeDialogueHelper.IsTeammateAlive<Salvador>(this))
        {
            DawnOfficeDialogueHelper.Speak(this, SelfDeathDefaultLine);
        }
        else
        {
            DawnOfficeDialogueHelper.Speak(this, SelfDeathAfterSalvadorDeathLine);
        }

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

        if (creature.Monster is Salvador)
        {
            bool philipRetreated = _hasObservedPhilipRetreat || DawnOfficeDialogueHelper.HasEscapedTeammate<Philip>(this);
            DawnOfficeDialogueHelper.Speak(
                this,
                philipRetreated ? AllyDeathSalvadorAfterPhilipRetreatLine : AllyDeathSalvadorBeforePhilipRetreatLine);
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var butterflySlash = new MoveState(
            "BUTTERFLY_SLASH",
            ButterflySlashMove,
            new MultiAttackIntent(ButterflySlashDamage, ButterflySlashHits),
            new DebuffIntent(strong: true));

        var eject = new MoveState(
            "EJECT",
            EjectMove,
            new CardDebuffIntent());

        var handleRequest = new MoveState(
            "HANDLE_REQUEST",
            HandleRequestMove,
            new DefendIntent(),
            new BuffIntent());

        eject.FollowUpState = butterflySlash;
        butterflySlash.FollowUpState = handleRequest;
        handleRequest.FollowUpState = butterflySlash;

        states.Add(butterflySlash);
        states.Add(eject);
        states.Add(handleRequest);

        return new MonsterMoveStateMachine(states, eject);
    }

    private async Task ButterflySlashMove(IReadOnlyList<Creature> targets)
    {
        var attack = await DamageCmd.Attack(ButterflySlashDamage)
            .FromMonster(this)
            .WithHitCount(ButterflySlashHits)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await ApplyBurnPerHitIgnoringBlock(AttackCommandCompat.Results(attack), ButterflySlashBurnPerHit);
    }

    private async Task EjectMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);

        foreach (Creature target in targets.Where(t => t.IsPlayer))
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaCostReductionPower>(target, EjectCardsPerTurn, Creature, null);
        }
    }

    private async Task HandleRequestMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await CreatureCmd.GainBlock(Creature, HandleRequestBlock, ValueProp.Move, null);
        await ApplyGuardToOtherLivingEnemies(HandleRequestGuardForAllies);
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

    private void SubscribeToCreatureChanges()
    {
        if (_isSubscribedToCreatureChanges || Creature.CombatState == null)
        {
            return;
        }

        Creature.CombatState.CreaturesChanged += OnCreaturesChanged;
        _isSubscribedToCreatureChanges = true;
    }

    private void UnsubscribeFromCreatureChanges()
    {
        if (!_isSubscribedToCreatureChanges || Creature.CombatState == null)
        {
            return;
        }

        Creature.CombatState.CreaturesChanged -= OnCreaturesChanged;
        _isSubscribedToCreatureChanges = false;
    }

    private void OnCreaturesChanged(CombatStateLike CombatState)
    {
        if (Creature.IsDead || _hasObservedPhilipRetreat || !ReferenceEquals(CombatState, Creature.CombatState))
        {
            return;
        }

        if (!DawnOfficeDialogueHelper.HasEscapedTeammate<Philip>(this))
        {
            return;
        }

        _hasObservedPhilipRetreat = true;
        DawnOfficeDialogueHelper.Speak(this, AllyDeathPhilipRetreatLine);
    }
}
