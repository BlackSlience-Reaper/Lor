using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.cards.BigBird;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.BigBird;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.BigBird;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.BigBird;
using LibraryOfRuina.visuals.BigBird;
using LibraryLib.Entities.Creatures;
using LibraryLib.Hooks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using BigBirdCreatureVisuals = LibraryOfRuina.visuals.BigBird.BigBirdCreatureVisuals;

namespace LibraryOfRuina.monsters.BigBird;

public sealed class BigBird : CounterIntentMonsterModel, ITargetedMonsterAttackProvider
{
    internal const string TextureRoot = "res://images/monsters/big_bird/";
    internal const string IdleTexturePath = TextureRoot + "idle.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";
    internal const string GuardTexturePath = TextureRoot + "guard.png";
    internal const string CharmTexturePath = TextureRoot + "charm.png";
    internal const string RescueOpenTexturePath = TextureRoot + "rescue_open.png";
    internal const string RescueCloseTexturePath = TextureRoot + "rescue_close.png";
    internal const string SleepTexturePath = TextureRoot + "sleep.png";

    internal const string VfxRoot = "res://images/vfx/";
    internal const string CharmedFilterTexturePath = VfxRoot + "big_bird_charmed_filter.png";
    internal const string RescueFilterFirstTexturePath = VfxRoot + "big_bird_rescue_filter_1.png";
    internal const string RescueFilterSecondTexturePath = VfxRoot + "big_bird_rescue_filter_2.png";

    internal const string SfxRoot = "res://audio/sfx/big_bird/";
    internal const string EyesLoopSfxPath = SfxRoot + "eyes_loop.ogg";
    internal const string MouthSfxPath = SfxRoot + "mouth.ogg";
    internal const string BeheadSfxPath = SfxRoot + "behead.ogg";
    internal const string CharmSfxPath = SfxRoot + "charm.ogg";

    internal const string PatrolMoveId = "BIG_BIRD_PATROL";
    internal const string CharmMoveId = "BIG_BIRD_CHARM";
    internal const string RescueMoveId = "BIG_BIRD_RESCUE";
    internal const string SleepMoveId = "BIG_BIRD_SLEEP";
    private const string RouterStateId = "BIG_BIRD_ROUTER";

    public const int CycleLength = 3;
    public const int PatrolBlock = 999;
    public const int CharmDamage = 1;
    public const int CharmBlock = 49;
    public const int CharmTurns = 2;
    public const int CharmBreakUnblockedDamage = 19;
    public const int SleepTurns = 2;
    public const int RescueDamage = 40;
    public const int RescueCharmedDamage = 60;

    private static readonly string PageRelicTitleLocKey = $"{ModelDb.GetId<BigBirdPageRelic>().Entry}.title";

    private static readonly string[] AdditionalAssetPaths =
    [
        CharmedFilterTexturePath,
        RescueFilterFirstTexturePath,
        RescueFilterSecondTexturePath,
        EyesLoopSfxPath,
        MouthSfxPath,
        BeheadSfxPath,
        CharmSfxPath,
        "res://images/powers/big_bird_ever_burning_lamp_passive_power.png",
        "res://images/powers/big_bird_charmed_power.png",
        "res://images/powers/big_bird_patrol_power.png",
        "res://images/powers/big_bird_sleep_power.png"
    ];

    private Dictionary<string, MoveState> _statesById = [];
    private int _cycleStep;
    private Creature? _plannedCharmTarget;
    private bool _counterTriggeredByLullaby;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
        _plannedCharmTarget = null;
    }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 697, 590);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 700, 593);

    public override int DefaultChaoResistance => 320;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override IEnumerable<string> AssetPaths =>
        BigBirdCreatureVisuals.Profile.AssetPaths
            .Concat(AdditionalAssetPaths)
            .Concat(CounterIntentVisuals.AssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _cycleStep = 0;
        _plannedCharmTarget = null;
        _counterTriggeredByLullaby = false;
        EncounterBgmController.RegisterMonster(Creature);
        //await PowerCmdCompat.Apply<BigBirdEverBurningLampPassivePower>(Creature, 1, Creature, null, silent: true);
        ForceRefreshMoveState();
    }

    public override void BeforeRemovedFromRoom()
    {
        BigBirdFilterOverlay.Clear();
        base.BeforeRemovedFromRoom();
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Creature)
        {
            await base.AfterDamageReceived(choiceContext, target, result, props, dealer, cardSource);
            return;
        }

        bool previous = _counterTriggeredByLullaby;
        _counterTriggeredByLullaby = cardSource is BirdLullabyCard;
        try
        {
            await base.AfterDamageReceived(choiceContext, target, result, props, dealer, cardSource);
        }
        finally
        {
            _counterTriggeredByLullaby = previous;
        }
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (side == CombatSide.Player)
        {
            var node = NCombatRoom.Instance!.GetCreatureNode(Creature);
            if (node?.Visuals is BigBirdCreatureVisuals visuals)
            {
                visuals.UpdateIdleForIntent(NextMove.Id);
            }
        }
        
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        MoveState patrol = Register(new MoveState(
            PatrolMoveId,
            PatrolMove,
            new CombinedDefendBuffIntent(
                PatrolBlock,
                "BIG_BIRD_PATROL.description")));

        MoveState charm = Register(new MoveState(
            CharmMoveId,
            CharmMove,
            new CombinedTargetedAttackDefendIntent(
                () => CharmDamage,
                () => 1,
                "BIG_BIRD_CHARM.description",
                "BIG_BIRD_CHARM.playerTargetDescription",
                useVanillaPlayerTargetIntentVisual: true,
                CharmBlock,
                IntentBadge.FromPower<BigBirdCharmedPower>(() => 1))));

        MoveState rescue = Register(new MoveState(
            RescueMoveId,
            RescueMove,
            new CombinedCounterAttackBuffIntent(
                () => BigBirdEncounterHelper.HasCharmedPlayer(Creature.CombatState) ? RescueCharmedDamage : RescueDamage,
                () => 1,
                "BIG_BIRD_RESCUE.description",
                PerformRescueCounter),
            new DebuffIntent()));

        MoveState sleep = Register(new MoveState(
            SleepMoveId,
            SleepMove,
            new SleepIntent()));

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng),
            shouldAppearInLogs: true);
        patrol.FollowUpState = router;
        charm.FollowUpState = router;
        rescue.FollowUpState = router;
        sleep.FollowUpState = router;

        return new MonsterMoveStateMachine([patrol, charm, rescue, sleep, router], router);
    }

    internal void ForceRefreshMoveState()
    {
        if (!IsMutable || MoveStateMachine == null || _statesById.Count == 0)
        {
            return;
        }

        string moveId = ResolvePlannedMoveId(RunRng.MonsterAi);
        if (_statesById.TryGetValue(moveId, out MoveState? state))
        {
            SetMoveImmediate(state, forceTransition: true);
        }
    }

    internal string ResolvePlannedMoveId(Rng rng)
    {
        Creature? creature = Creature;
        if (creature?.GetPower<BigBirdSleepPower>() != null)
        {
            return SleepMoveId;
        }

        return ResolveAwakeMoveId();
    }

    private string ResolveAwakeMoveId()
    {
        Creature? creature = Creature;

        bool hasCharmedPlayer = creature?.CombatState != null
            && BigBirdEncounterHelper.HasCharmedPlayer(creature.CombatState);

        return _cycleStep switch
        {
            0 => hasCharmedPlayer ? PatrolMoveId : CharmMoveId,
            2 => hasCharmedPlayer
                ? (CanUseRescueMove(creature) ? RescueMoveId : PatrolMoveId)
                : CharmMoveId,
            1 => hasCharmedPlayer ? PatrolMoveId : CharmMoveId,
            _ => PatrolMoveId
        };
    }

    internal async Task EnterSleepConfusion()
    {
        if (Creature.IsDead
            || Creature is not LibraryCreature libraryCreature
            || !libraryCreature.HasChaoResistance
            || libraryCreature.CombatState is not { } combatState)
        {
            return;
        }

        decimal previousChaoValue = libraryCreature.CurrentChaoValue;
        libraryCreature.SetCurrentChaoValueInternal(0m);
        decimal changedAmount = libraryCreature.CurrentChaoValue - previousChaoValue;
        if (changedAmount != 0m)
        {
            await LibraryHooks.AfterCurrentChaoValueChanged(
                libraryCreature.Player?.RunState ?? combatState.RunState,
                combatState,
                libraryCreature,
                changedAmount,
                LibraryDamageType.None);
        }

        if (libraryCreature.CurrentChaoValue == 0
            && !libraryCreature.IsChaoed
            && libraryCreature.MaxChaoValue != 0)
        {
            await LibraryCreatureCmd.Stun(libraryCreature, ResolveAwakeMoveId());
        }
    }

    private static bool CanUseRescueMove(Creature? creature)
    {
        return creature?.CombatState != null
            && creature.CombatState.RoundNumber > 3
            && BigBirdEncounterHelper.HasCharmedPlayer(creature.CombatState);
    }

    public bool UsesTargetedAttackContract(Creature owner) => true;

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        if (NextMove.Id == RescueMoveId)
        {
            Creature? charmed = BigBirdEncounterHelper.FirstCharmedPlayer(owner.CombatState);
            if (charmed != null)
            {
                return [charmed];
            }
        }

        if (NextMove.Id == CharmMoveId)
        {
            return new[] { ResolveCharmPreviewTarget(owner) }.Where(static creature => creature != null).Cast<Creature>().ToArray();
        }

        return BigBirdEncounterHelper.LivingPlayers(owner.CombatState);
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        return GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "Unknown Target";
    }

    private async Task PatrolMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.25f);
        await CreatureCmd.GainBlock(Creature, PatrolBlock, ValueProp.Move, null);

        AdvanceCycle();
    }

    private async Task CharmMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = ResolveCharmTarget();
        _plannedCharmTarget = target;
        LocalOggOneShotPlayer.Play(CharmSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Charm", 0.5f);
        await CreatureCmd.GainBlock(Creature, CharmBlock, ValueProp.Move, null);

        if (target is { IsAlive: true })
        {
            using (TargetedMonsterAttackHelper.ForceTargets(Creature, [target]))
            {
                await DamageCmd.Attack(CharmDamage)
                    .FromMonster(this)
                    .WithAttackerAnim("Charm", 0.2f)
                    .Execute(null);
            }

            await ApplyCharm(target);
        }

        _plannedCharmTarget = null;
        AdvanceCycle();
    }

    private Task RescueMove(IReadOnlyList<Creature> targets)
    {
        PrepareCounterIntentsFromCurrentMove();
        AdvanceCycle();
        return Task.CompletedTask;
    }

    private async Task SleepMove(IReadOnlyList<Creature> targets)
    {
        await BigBirdEncounterHelper.ClearAllCharmedPlayers(new ThrowingPlayerChoiceContext(), Creature.CombatState);
        await CreatureCmd.TriggerAnim(Creature, "Sleep", 0.5f);
    }

    private async Task PerformRescueCounter(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        Creature? target = BigBirdEncounterHelper.FirstCharmedPlayer(owner.CombatState) ?? counterTarget;
        if (target.IsDead || owner.IsDead || owner.Monster is not BigBird)
        {
            return;
        }

        await BigBirdFilterOverlay.PlayRescueSequence();
        LocalOggOneShotPlayer.Play(MouthSfxPath, -2f);
        int damage = _counterTriggeredByLullaby
            ? 0
            : target.GetPower<BigBirdCharmedPower>() != null ? RescueCharmedDamage : RescueDamage;

        if (damage > 0)
        {
            using (TargetedMonsterAttackHelper.ForceTargets(owner, [target]))
            {
                await DamageCmd.Attack(damage)
                    .FromMonster(this)
                    .WithAttackerAnim("Rescue", 0.25f)
                    .Execute(choiceContext);
            }
        }
        else
        {
            await CreatureCmd.TriggerAnim(owner, "Rescue", 0.25f);
        }

        LocalOggOneShotPlayer.Play(BeheadSfxPath, -2f);
    }

    private async Task ApplyCharm(Creature target)
    {
        await PowerCmdCompat.SetAmount<BigBirdCharmedPower>(target, CharmTurns, Creature, null);
        BigBirdFilterOverlay.RefreshForCombat(target.CombatState);
    }

    private Creature? ResolveCharmPreviewTarget(Creature owner)
    {
        return _plannedCharmTarget is { IsAlive: true } planned
            ? planned
            : BigBirdEncounterHelper.LivingPlayers(owner.CombatState).FirstOrDefault();
    }

    private Creature? ResolveCharmTarget()
    {
        IReadOnlyList<Creature> players = BigBirdEncounterHelper.LivingPlayers(Creature.CombatState);
        return players.Count == 0
            ? null
            : RunRng.MonsterAi.NextItem(players);
    }

    private void AdvanceCycle()
    {
        _cycleStep = (_cycleStep + 1) % CycleLength;
    }

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

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

    private void AddPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !BigBirdEncounterHelper.IsBigBirdEncounter(deadCreature.CombatState))
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<BigBirdPageRelic>(
                room,
                player,
                PageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<BigBirdPageRelic>().ToMutable(), player));
        }
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            AddPageRewardsFromDeathHook(creature);
        }

        return Task.CompletedTask;
    }

}

public sealed class EyeballBird : LorMonsterModel
{
    internal const string TextureRoot = "res://images/monsters/eyeball_bird/";
    internal const string IdleTexturePath = TextureRoot + "idle.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";
    internal const string AttackTexturePath = TextureRoot + "attack.png";
    internal const string EvadeTexturePath = TextureRoot + "evade.png";

    internal const string SfxRoot = "res://audio/sfx/eyeball_bird/";
    internal const string AttackSfxPath = SfxRoot + "attack.ogg";

    private const string HopMoveId = "EYEBALL_BIRD_HOP";
    private const string PeckMoveId = "EYEBALL_BIRD_PECK";
    private const string GlareMoveId = "EYEBALL_BIRD_GLARE";
    private const string PatrolMoveId = "EYEBALL_BIRD_PATROL";
    private const string RouterStateId = "EYEBALL_BIRD_ROUTER";

    private const int HopBlock = 25;
    private const int HopFrail = 1;
    private const int PeckHits = 2;
    private const int GlareBind = 12;
    private const int PatrolStrength = 5;

    private static readonly string[] AdditionalAssetPaths =
    [
        AttackSfxPath,
        "res://images/powers/eyeball_bird_sleepy_eyes_passive_power.png",
        "res://images/powers/eyeball_bird_follow_passive_power.png"
    ];

    private Dictionary<string, MoveState> _statesById = [];
    private string? _lastMoveId;
    private Player? _lastPlayerDamageDealer;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
        _lastPlayerDamageDealer = null;
    }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 196, 190);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 194);

    public override int DefaultChaoResistance => 110;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Vulnerable,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override IEnumerable<string> AssetPaths =>
        EyeballBirdCreatureVisuals.Profile.AssetPaths
            .Concat(AdditionalAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _lastMoveId = null;
        _lastPlayerDamageDealer = null;
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1, Creature, null, true);
        await PowerCmdCompat.Apply<EyeballBirdSleepyEyesPassivePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<EyeballBirdFollowPassivePower>(Creature, 1, Creature, null, silent: true);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        await base.AfterDamageReceived(choiceContext, target, result, props, dealer, cardSource);
        if (target == Creature && result.TotalDamage > 0)
        {
            RememberPlayerDamageDealer(dealer);
        }
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (target == Creature && result.TotalDamage > 0)
        {
            RememberPlayerDamageDealer(dealer);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature || wasRemovalPrevented)
        {
            return;
        }

        if (_lastPlayerDamageDealer?.Creature.IsAlive == true)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<BirdLullabyCard>(
                _lastPlayerDamageDealer.Creature,
                PileType.Hand,
                1,
                addedByPlayer: false,
                CardPilePosition.Top);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        MoveState hop = Register(new MoveState(
            HopMoveId,
            HopMove,
            new CombinedDefendDebuffIntent(
                HopBlock,
                "EYEBALL_BIRD_HOP.description",
                IntentBadge.FromPower<FrailPower>(() => HopFrail))));

        MoveState peck = Register(new MoveState(
            PeckMoveId,
            PeckMove,
            new DynamicAttackIntent(() => GetPeckDamageRoll(), () => PeckHits)));

        MoveState glare = Register(new MoveState(
            GlareMoveId,
            GlareMove,
            new CombinedAttackDebuffIntent(
                () => GetGlareDamageRoll(),
                () => 1,
                "EYEBALL_BIRD_GLARE.description",
                IntentBadge.FromPower<LibraryBindingPower>(() => GlareBind))));

        MoveState patrol = Register(new MoveState(
            PatrolMoveId,
            PatrolMove,
            new CombinedAttackBuffIntent(
                () => GetPatrolDamageRoll(),
                () => 1,
                "EYEBALL_BIRD_PATROL.description",
                IntentBadge.FromPower<StrengthPower>(() => PatrolStrength))));

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng));
        hop.FollowUpState = router;
        peck.FollowUpState = router;
        glare.FollowUpState = router;
        patrol.FollowUpState = router;

        return new MonsterMoveStateMachine([hop, peck, glare, patrol, router], router);
    }

    internal string ResolvePlannedMoveId(Rng rng)
    {
        bool hasCharmedPlayer = Creature?.CombatState != null
            && BigBirdEncounterHelper.HasCharmedPlayer(Creature.CombatState);
        string[] candidates = hasCharmedPlayer
            ? [HopMoveId]
            : [HopMoveId, PeckMoveId, GlareMoveId, PatrolMoveId];
        string[] filtered = candidates.Where(move => move != _lastMoveId).ToArray();
        string selected = rng.NextItem(filtered.Length > 0 ? filtered : candidates) ?? candidates[0];
        return selected;
    }

    private async Task HopMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Evade", 0.34f);
        await CreatureCmd.GainBlock(Creature, HopBlock, ValueProp.Move, null);
        foreach (Creature player in targets)
        {
            await PowerCmdCompat.ApplyDebuff<FrailPower>(player, HopFrail, Creature, null);
        }

        FinishMove(HopMoveId);
    }

    private async Task PeckMove(IReadOnlyList<Creature> targets)
    {
        int damage = GetPeckDamageRoll();
        IReadOnlyList<Creature> actualTargets = GetFollowTargets();
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets))
        {
            await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithHitCount(PeckHits)
                .WithAttackerAnim("Attack", 0.28f)
                .Execute(null);
        }

        FinishMove(PeckMoveId);
    }

    private async Task GlareMove(IReadOnlyList<Creature> targets)
    {
        int damage = GetGlareDamageRoll();
        IReadOnlyList<Creature> actualTargets = GetFollowTargets();
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        AttackCommand command;
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets))
        {
            command = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.28f)
                .Execute(null);
        }

        foreach (Creature target in AttackCommandCompat.Results(command)
            .Where(static result => result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct())
        {
            await PowerCmdCompat.Apply<LibraryBindingPower>(target, GlareBind, Creature, null);
        }

        FinishMove(GlareMoveId);
    }

    private async Task PatrolMove(IReadOnlyList<Creature> targets)
    {
        int damage = GetPatrolDamageRoll();
        IReadOnlyList<Creature> actualTargets = GetFollowTargets();
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets))
        {
            await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.28f)
                .Execute(null);
        }

        await PowerCmdCompat.Apply<StrengthPower>(Creature, PatrolStrength, Creature, null);

        FinishMove(PatrolMoveId);
    }

    private void RememberPlayerDamageDealer(Creature? dealer)
    {
        Player? player = dealer?.Player ?? dealer?.PetOwner;
        if (player?.Creature.IsAlive == true)
        {
            _lastPlayerDamageDealer = player;
        }
    }

    private IReadOnlyList<Creature> GetFollowTargets()
    {
        return BigBirdEncounterHelper.LivingPlayers(Creature.CombatState)
            .Where(static creature => creature.GetPower<BigBirdCharmedPower>() == null)
            .ToArray();
    }

    private void FinishMove(string moveId)
    {
        _lastMoveId = moveId;
    }

    private int GetPeckDamageRoll() => GetAscensionDamage(lowAscension: 11, highAscension: 13);

    private int GetGlareDamageRoll() => GetAscensionDamage(lowAscension: 22, highAscension: 26);

    private int GetPatrolDamageRoll() => GetAscensionDamage(lowAscension: 16, highAscension: 19);

    private static int GetAscensionDamage(int lowAscension, int highAscension) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, highAscension, lowAscension);

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

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
