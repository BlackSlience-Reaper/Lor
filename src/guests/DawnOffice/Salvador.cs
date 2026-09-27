using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.DawnOffice;
using LibraryOfRuina.visuals.DawnOffice;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.guests.DawnOffice;

public sealed class Salvador : MonsterModel
{
    private static readonly string[] BattleStartLines =
    {
        "SALVADOR.dialogue.battleStart.0",
        "SALVADOR.dialogue.battleStart.1",
        "SALVADOR.dialogue.battleStart.2"
    };

    private static readonly string[] VictoryLines =
    {
        "SALVADOR.dialogue.victory.0",
        "SALVADOR.dialogue.victory.1"
    };

    private static readonly string[] EnemyDeathLines =
    {
        "SALVADOR.dialogue.enemyDeath.0",
        "SALVADOR.dialogue.enemyDeath.1",
        "SALVADOR.dialogue.enemyDeath.2"
    };

    private const string VictoryOnlySelfLine = "SALVADOR.dialogue.victory.onlySelf";
    private const string SelfDeathDefaultLine = "SALVADOR.dialogue.selfDeath.default";
    private const string SelfDeathAfterYunaDeathLine = "SALVADOR.dialogue.selfDeath.afterYunaDeath";
    private const string AllyDeathYunaBeforePhilipRetreatLine = "SALVADOR.dialogue.allyDeath.yunaBeforePhilipRetreat";
    private const string AllyDeathYunaAfterPhilipRetreatLine = "SALVADOR.dialogue.allyDeath.yunaAfterPhilipRetreat";
    private const string AllyDeathPhilipRetreatLine = "SALVADOR.dialogue.allyDeath.philipRetreat";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 90, 88);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 90, 88);

    public override IEnumerable<string> AssetPaths =>
        SalvadorCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private const int CrackOfDawnDamage = 14;
    private const int CrackOfDawnBurn = 6;
    private const int CrackOfDawnGuardForAllies = 1;
    private const int FlashOfSunupWeak = 1;
    private const int FlashOfSunupBurnCards = 2;
    private const int SunsetBladeDamage = 2;
    private const int SunsetBladeHits = 4;
    private const int SunsetBladeBurnPerHit = 1;
    private const int HandleRequestBlock = 10;
    private const int HandleRequestStrength = 1;
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
        await PowerCmdCompat.Apply<LibraryOfRuinaDawnFirePower>(Creature, 1, Creature, null, silent: true);
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

        if (DawnOfficeDialogueHelper.IsTeammateAlive<Yuna>(this))
        {
            DawnOfficeDialogueHelper.Speak(this, SelfDeathDefaultLine);
        }
        else
        {
            DawnOfficeDialogueHelper.Speak(this, SelfDeathAfterYunaDeathLine);
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

        if (creature.Monster is Yuna)
        {
            bool philipRetreated = _hasObservedPhilipRetreat || DawnOfficeDialogueHelper.HasEscapedTeammate<Philip>(this);
            DawnOfficeDialogueHelper.Speak(
                this,
                philipRetreated ? AllyDeathYunaAfterPhilipRetreatLine : AllyDeathYunaBeforePhilipRetreatLine);
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var crackOfDawn = new MoveState(
            "CRACK_OF_DAWN",
            CrackOfDawnMove,
            new SingleAttackIntent(CrackOfDawnDamage),
            new DebuffIntent(strong: true),
            new BuffIntent());

        var flashOfSunup = new MoveState(
            "FLASH_OF_SUNUP",
            FlashOfSunupMove,
            new DebuffIntent(),
            new DetailedStatusCardIntent<Burn>(
                FlashOfSunupBurnCards,
                PileType.Hand,
                showSingleTargetMarker: false));

        var sunsetBlade = new MoveState(
            "SUNSET_BLADE",
            SunsetBladeMove,
            new MultiAttackIntent(SunsetBladeDamage, SunsetBladeHits),
            new DebuffIntent(strong: true));

        var handleRequest = new MoveState(
            "HANDLE_REQUEST",
            HandleRequestMove,
            new DefendIntent(),
            new BuffIntent());

        crackOfDawn.FollowUpState = flashOfSunup;
        flashOfSunup.FollowUpState = sunsetBlade;
        sunsetBlade.FollowUpState = handleRequest;
        handleRequest.FollowUpState = crackOfDawn;

        var opening = new RandomBranchState("OPENING");
        opening.AddBranch(flashOfSunup, MoveRepeatType.CannotRepeat, 1f);
        opening.AddBranch(sunsetBlade, MoveRepeatType.CannotRepeat, 1f);

        states.Add(crackOfDawn);
        states.Add(flashOfSunup);
        states.Add(sunsetBlade);
        states.Add(handleRequest);
        states.Add(opening);

        return new MonsterMoveStateMachine(states, opening);
    }

    private async Task CrackOfDawnMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(CrackOfDawnDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await PowerCmdCompat.Apply<LibraryBurnPower>(targets, CrackOfDawnBurn, Creature, null);
        await ApplyGuardToOtherLivingEnemies(CrackOfDawnGuardForAllies);
    }

    private async Task FlashOfSunupMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await PowerCmdCompat.Apply<WeakPower>(targets, FlashOfSunupWeak, Creature, null);
        foreach (Creature target in targets)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Burn>(target, PileType.Hand, FlashOfSunupBurnCards, addedByPlayer: false);
        }
    }

    private async Task SunsetBladeMove(IReadOnlyList<Creature> targets)
    {
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
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.675f);
        await CreatureCmd.GainBlock(Creature, HandleRequestBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, HandleRequestStrength, Creature, null);
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
