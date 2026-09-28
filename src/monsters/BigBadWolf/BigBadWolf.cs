using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.BigBadWolf;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.BigBadWolf;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.BigBadWolf;
using LibraryOfRuina.visuals.BigBadWolf;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.monsters.BigBadWolf;

public sealed class BigBadWolf : LorMonsterModel
{
    private const string Root = "res://images/monsters/big_bad_wolf/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string StrikeTexturePath = Root + "strike.png";
    public const string SlashTexturePath = Root + "slash.png";
    public const string SwallowTexturePath = Root + "swallow.png";
    public const string SwallowedStrikeTexturePath = Root + "swallowed_strike.png";
    public const string SwallowedSlashTexturePath = Root + "swallowed_slash.png";
    public const string SwallowedHitTexturePath = Root + "swallowed_hit.png";

    public const string AttackSfxPath = "res://audio/sfx/big_bad_wolf/attack.ogg";
    public const string ChangeSfxPath = "res://audio/sfx/big_bad_wolf/change.ogg";
    public const string SpitSfxPath = "res://audio/sfx/big_bad_wolf/spit.ogg";

    private const string SnoreMoveId = "SNORE";
    private const string SwallowMoveId = "SWALLOW";
    private const string SwallowOrEscapeStateId = "SWALLOW_OR_ESCAPE";
    private const string EscapeMoveId = "ESCAPE";
    private const string BigClawsMoveId = "BIG_CLAWS";
    private const string WolfComesMoveId = "WOLF_COMES";

    private const int SnoreBlock = 36;
    private const int SnoreBaseDamage = 14;
    private const int SnoreHighAscensionDamage = 17;
    private const int BigClawsBaseDamage = 10;
    private const int BigClawsHighAscensionDamage = 11;
    private const int BigClawsHits = 2;
    private const int BigClawsBleed = 9;
    public const int SpitOutHpLossPercent = 20;
    public const int BornToBeNextTurnStrength = 3;
    private const int SwallowBaseDamage = 23;
    private const int SwallowHighAscensionDamage = 24;
    private const int EatenCardsBeforeEscape = 3;
    private const int WolfComesBaseDamage = 9;
    private const int WolfComesHighAscensionDamage = 11;
    private const int TemporaryThorns = 8;
    private const float AttackAnimDelaySeconds = 0.42f;

    private static readonly string BigBadWolfPageRelicTitleLocKey =
        $"{ModelDb.GetId<BigBadWolfPageRelic>().Entry}.title";

    private static readonly Func<CardModel, bool>[] StealPriorities =
    [
        static card => card.Enchantment is not Imbued && card.Rarity == CardRarity.Uncommon,
        static card => card.Enchantment is not Imbued
            && card.Rarity is CardRarity.Common or CardRarity.Rare or CardRarity.Event,
        static card => card.Enchantment is not Imbued
            && card.Rarity is CardRarity.Basic or CardRarity.Quest,
        static card => card.Rarity == CardRarity.Ancient || card.Enchantment is Imbued
    ];

    private List<PendingStolenCard> _pendingCards = [];
    private Dictionary<ulong, int> _eatenCardCountsByPlayerId = [];
    private bool _waitingForPlayerWindow;
    private bool _vulnerabilityWindowActive;
    private bool _resistanceOverridden;
    private bool _isResolvingPendingCard;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pendingCards = [.. _pendingCards];
        _eatenCardCountsByPlayerId = new(_eatenCardCountsByPlayerId);
    }

    public bool HasPendingCard => _pendingCards.Count > 0;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 502, 490);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 505, 498);

    public override int DefaultChaoResistance => 80;

    // Doom removes the creature node before the Kill chain runs. Keep the
    // node alive so swallowed-card cleanup and death rewards still execute
    // against a valid creature node.
    public override bool ShouldDisappearFromDoom => false;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };

    private int SnoreDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, SnoreHighAscensionDamage, SnoreBaseDamage);

    private int BigClawsDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            BigClawsHighAscensionDamage,
            BigClawsBaseDamage);

    private int SwallowDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            SwallowHighAscensionDamage,
            SwallowBaseDamage);

    private int WolfComesDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            WolfComesHighAscensionDamage,
            WolfComesBaseDamage);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                BigBadWolfCreatureVisuals
                    .Profile.AssetPaths)
            {
                AttackSfxPath,
                ChangeSfxPath,
                SpitSfxPath
            };

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
        await ClearPendingCard();
        _eatenCardCountsByPlayerId.Clear();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<BigBadWolfPunishEvilPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<BigBadWolfBornToBePower>(Creature, 1m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
        ClearStolenCardMarker();
        ClearPendingCardStateOnly();
    }

    public override async Task BeforeDeath(Creature creature)
    {
        if (creature != Creature || !HasPendingCard || _isResolvingPendingCard)
        {
            return;
        }

        try
        {
            ReturnPendingCardsAsRewards();
            await ClearPendingCard(resetVisuals: true);
        }
        catch (Exception exception)
        {
            Log.Warn("[BigBadWolf] failed to return swallowed card during death cleanup: " + exception);
            ClearPendingCardStateOnly();
        }
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return Task.CompletedTask;
        }

        AddBigBadWolfPageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    private void AddBigBadWolfPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || room.Encounter is not BigBadWolfWeak)
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<BigBadWolfPageRelic>(
                room,
                player,
                BigBadWolfPageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(
                player,
                new RelicReward(ModelDb.Relic<BigBadWolfPageRelic>().ToMutable(), player));
        }
    }

    public override Task AfterCurrentChaoValueChanged(Creature target, decimal amount, LibraryDamageType type)
    {
        if (target != Creature
            || amount >= 0m
            || !HasPendingCard
            || !_vulnerabilityWindowActive
            || target is not LibraryCreature lc
            || lc.CurrentChaoValue > 0)
        {
            return Task.CompletedTask;
        }

        return SpitPendingCard(new BlockingPlayerChoiceContext());
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var snore = new MoveState(
            SnoreMoveId,
            SnoreMove,
            new DefendIntent(),
            new BadgedAttackIntent(() => SnoreDamage, null, IntentBadge.Heal()));

        var swallow = new MoveState(
            SwallowMoveId,
            SwallowMove,
            new SingleAttackIntent(() => SwallowDamage),
            new CardDebuffIntent());

        var escape = new MoveState(
            EscapeMoveId,
            EscapeMove,
            new EscapeIntent());

        var swallowOrEscape = new ConditionalBranchState(SwallowOrEscapeStateId);
        swallowOrEscape.AddState(escape, HasReachedEatenCardLimit);
        swallowOrEscape.AddState(swallow, () => true);

        var bigClaws = new MoveState(
            BigClawsMoveId,
            BigClawsMove,
            new BadgedAttackIntent(() => BigClawsDamage, () => BigClawsHits, IntentBadge.Bleed(BigClawsBleed)));

        var wolfComes = new MoveState(
            WolfComesMoveId,
            WolfComesMove,
            new SingleAttackIntent(() => WolfComesDamage),
            new DetailedBuffIntent<ThornsPower>(TemporaryThorns));

        snore.FollowUpState = swallowOrEscape;
        swallow.FollowUpState = bigClaws;
        escape.FollowUpState = escape;
        bigClaws.FollowUpState = wolfComes;
        wolfComes.FollowUpState = snore;

        return new MonsterMoveStateMachine([snore, swallow, escape, swallowOrEscape, bigClaws, wolfComes], snore);
    }

    public async Task BeginSwallowVulnerabilityWindow(PlayerChoiceContext choiceContext)
    {
        if (!HasPendingCard || !_waitingForPlayerWindow || Creature is not LibraryCreature lc)
        {
            return;
        }

        _waitingForPlayerWindow = false;
        _vulnerabilityWindowActive = true;
        _resistanceOverridden = true;

        await SetAllChaoResistances(choiceContext, lc, LibraryResistanceLevel.Fatal);
    }

    public async Task ResolveBornToBe(PlayerChoiceContext choiceContext)
    {
        if (!HasPendingCard || Creature.IsDead)
        {
            return;
        }

        _isResolvingPendingCard = true;
        try
        {
            LocalOggOneShotPlayer.Play(ChangeSfxPath, -2f);
            await CreatureCmd.TriggerAnim(Creature, "Eat", 0.35f);
            await RestoreBaseChaoResistance(choiceContext);
            RecordEatenPendingCards();
            await ClearPendingCard(removeSwipeMarker: true, resetVisuals: false);
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(
                choiceContext,
                Creature,
                BornToBeNextTurnStrength,
                Creature,
                null);
        }
        finally
        {
            _isResolvingPendingCard = false;
        }
    }

    private async Task SnoreMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(Creature, SnoreBlock, ValueProp.Move, null);
        IReadOnlyList<DamageResult> results = await ExecuteAttackSegment(
            "Strike",
            SnoreDamage,
            "vfx/vfx_attack_blunt");

        int heal = results.Sum(static result => Math.Max(0, result.UnblockedDamage));
        if (heal > 0)
        {
            await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, heal));
        }
    }

    private async Task BigClawsMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < BigClawsHits; i++)
        {
            if (Creature.IsDead) return;
            IReadOnlyList<DamageResult> results = await ExecuteAttackSegment(
                "Slash",
                BigClawsDamage,
                "vfx/vfx_attack_slash");

            IReadOnlyList<Creature> bleedTargets = results
                .Where(static result => result.Receiver.IsPlayer && result.Receiver.IsAlive && result.UnblockedDamage > 0)
                .Select(static result => result.Receiver)
                .Distinct()
                .ToList();

            if (bleedTargets.Count > 0)
            {
                await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, BigClawsBleed, Creature, null);
            }
        }
    }

    private async Task SwallowMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment(
            "Swallow",
            SwallowDamage,
            "vfx/vfx_attack_blunt");

        await StealPendingCards(targets);
    }

    private async Task EscapeMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.Escape(Creature);
    }

    private async Task WolfComesMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment(
            "Strike",
            WolfComesDamage,
            "vfx/vfx_attack_slash");

        await PowerCmdCompat.Apply<ThornsPower>(Creature, TemporaryThorns, Creature, null);
        await PowerCmdCompat.Apply<BigBadWolfTemporaryThornsPower>(Creature, TemporaryThorns, Creature, null, silent: true);
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteAttackSegment(
        string trigger,
        int damage,
        string hitFx)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        AttackCommand command = await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(trigger, AttackAnimDelaySeconds)
            .WithHitFx(hitFx)
            .Execute(null);

        return AttackCommandCompat.Results(command);
    }

    private async Task StealPendingCards(IReadOnlyList<Creature> targets)
    {
        if (HasPendingCard)
        {
            return;
        }

        List<(Player Owner, List<CardModel> Cards)> candidates = GetStealCandidates(targets);
        if (candidates.Count == 0)
        {
            return;
        }

        foreach ((Player owner, List<CardModel> cards) in candidates)
        {
            await StealPendingCard(owner, cards);
        }

        if (!HasPendingCard)
        {
            return;
        }

        _waitingForPlayerWindow = true;
        _vulnerabilityWindowActive = false;
        _resistanceOverridden = false;
        ShowLocalStolenCardMarker();
        await CreatureCmd.TriggerAnim(Creature, "Swallowed", 0f);
    }

    private async Task StealPendingCard(Player owner, IReadOnlyList<CardModel> cards)
    {
        CardModel? cardToSteal = SelectCardToSteal(cards);
        CardModel? deckCard = cardToSteal?.DeckVersion;
        if (cardToSteal == null || deckCard == null)
        {
            return;
        }

        if (!IsStealableDeckCard(deckCard, owner))
        {
            Log.Warn("[BigBadWolf] skipped swallowing card with invalid deck version: combatCard="
                + cardToSteal.Id
                + ", deckCard="
                + deckCard.Id
                + ", owner="
                + owner);
            return;
        }

        try
        {
            await CardPileCmd.RemoveFromDeck(deckCard, showPreview: false);
        }
        catch (Exception exception)
        {
            Log.Warn("[BigBadWolf] skipped swallowing card because deck removal failed: combatCard="
                + cardToSteal.Id
                + ", deckCard="
                + deckCard.Id
                + ", owner="
                + owner
                + ", error="
                + exception);
            return;
        }

        await CardPileCmd.RemoveFromCombat(cardToSteal);
        SwipePower swipe = (SwipePower)ModelDb.Power<SwipePower>().ToMutable();
        swipe.Target = owner.Creature;
        swipe.StolenCard = cardToSteal;
        await PowerCmdCompat.Apply(swipe, Creature, 1m, Creature, null);
        _pendingCards.Add(new PendingStolenCard(cardToSteal, deckCard, owner, swipe));
    }

    private static bool IsStealableDeckCard(CardModel deckCard, Player owner)
    {
        return deckCard.Owner == owner
            && deckCard.Pile?.Type == PileType.Deck
            && !deckCard.HasBeenRemovedFromState
            && owner.RunState.ContainsCard(deckCard);
    }

    private static List<(Player Owner, List<CardModel> Cards)> GetStealCandidates(IEnumerable<Creature> targets)
    {
        return targets
            .Where(static target => target.IsAlive)
            .Select(static target => target.Player ?? target.PetOwner)
            .Where(static owner => owner != null)
            .Distinct()
            .OrderBy(static owner => owner!.NetId)
            .Select(static owner => (Owner: owner!, Cards: GetStealableCards(owner!)))
            .Where(static candidate => candidate.Cards.Count > 0)
            .ToList();
    }

    private static List<CardModel> GetStealableCards(Player owner)
    {
        return CardPile.GetCards(owner, PileType.Draw, PileType.Discard)
            .Where(static card => card.DeckVersion != null)
            .ToList();
    }

    private CardModel? SelectCardToSteal(IReadOnlyList<CardModel> cards)
    {
        if (cards.Count == 0)
        {
            return null;
        }

        IEnumerable<CardModel> candidates = cards;
        foreach (Func<CardModel, bool> priority in StealPriorities)
        {
            List<CardModel> prioritized = cards.Where(priority).ToList();
            if (prioritized.Count > 0)
            {
                candidates = prioritized;
                break;
            }
        }

        return RunRng.CombatCardGeneration.NextItem(candidates);
    }

    private bool HasReachedEatenCardLimit()
    {
        return _eatenCardCountsByPlayerId.Count > 0
            && _eatenCardCountsByPlayerId.Values.Max() >= EatenCardsBeforeEscape;
    }

    private void RecordEatenPendingCards()
    {
        foreach (PendingStolenCard pending in _pendingCards)
        {
            ulong playerId = pending.Owner.NetId;
            _eatenCardCountsByPlayerId.TryGetValue(playerId, out int current);
            _eatenCardCountsByPlayerId[playerId] = current + 1;
        }
    }

    private async Task SpitPendingCard(PlayerChoiceContext choiceContext)
    {
        if (!HasPendingCard)
        {
            return;
        }

        PendingStolenCard[] pendingCards = _pendingCards.ToArray();
        CombatStateLike? combatState = Creature.CombatState;

        _isResolvingPendingCard = true;
        try
        {
            LocalOggOneShotPlayer.Play(SpitSfxPath, -2f);
            await CreatureCmd.TriggerAnim(Creature, "Spit", 0.35f);
            await RestoreBaseChaoResistance(choiceContext);
            await RemoveSwipeMarkerPower();

            int hpLoss = Math.Max(1, (int)Math.Ceiling(Creature.MaxHp * SpitOutHpLossPercent / 100m));
            await CreatureCmd.SetCurrentHp(Creature, Math.Max(0m, Creature.CurrentHp - hpLoss));

            foreach (PendingStolenCard pending in pendingCards)
            {
                RestorePendingDeckCardToRunState(pending.DeckCard, pending.Owner);
                await CardPileCmd.Add(pending.DeckCard, PileType.Deck, CardPilePosition.Bottom, this, true);

                if (combatState != null && pending.Owner.Creature.IsAlive)
                {
                    CardModel handCard = combatState.CloneCard(pending.DeckCard);
                    handCard.DeckVersion = pending.DeckCard;
                    await CardPileCmd.Add(handCard, PileType.Hand, CardPilePosition.Bottom, this);
                }
            }

            await ClearPendingCard(removeSwipeMarker: false, resetVisuals: false);
        }
        finally
        {
            _isResolvingPendingCard = false;
        }
    }

    private void ReturnPendingCardsAsRewards()
    {
        if (Creature.CombatState?.RunState.CurrentRoom is not CombatRoom room)
        {
            return;
        }

        foreach (PendingStolenCard pending in _pendingCards)
        {
            RestorePendingDeckCardToRunState(pending.DeckCard, pending.Owner);
            var reward = new SpecialCardReward(pending.DeckCard, pending.Owner);
            reward.SetCustomDescriptionEncounterSource(ModelDb.Encounter<BigBadWolfWeak>().Id);
            room.AddExtraReward(pending.Owner, reward);
        }
    }

    private static void RestorePendingDeckCardToRunState(CardModel deckCard, Player owner)
    {
        if (!owner.RunState.ContainsCard(deckCard) || deckCard.HasBeenRemovedFromState)
        {
            owner.RunState.AddCard(deckCard, owner);
        }
    }

    private async Task RestoreBaseChaoResistance(PlayerChoiceContext choiceContext)
    {
        if (!_resistanceOverridden || Creature is not LibraryCreature lc)
        {
            return;
        }

        await LibraryCreatureCmd.SetChaoResistance(choiceContext, lc, Creature, LibraryDamageType.Slash, LibraryResistanceLevel.Endure);
        await LibraryCreatureCmd.SetChaoResistance(choiceContext, lc, Creature, LibraryDamageType.Pierce, LibraryResistanceLevel.Endure);
        await LibraryCreatureCmd.SetChaoResistance(choiceContext, lc, Creature, LibraryDamageType.Blunt, LibraryResistanceLevel.Endure);
        _resistanceOverridden = false;
    }

    private static async Task SetAllChaoResistances(
        PlayerChoiceContext choiceContext,
        LibraryCreature target,
        LibraryResistanceLevel resistanceLevel)
    {
        await LibraryCreatureCmd.SetChaoResistance(choiceContext, target, target, LibraryDamageType.Slash, resistanceLevel);
        await LibraryCreatureCmd.SetChaoResistance(choiceContext, target, target, LibraryDamageType.Pierce, resistanceLevel);
        await LibraryCreatureCmd.SetChaoResistance(choiceContext, target, target, LibraryDamageType.Blunt, resistanceLevel);
    }

    private async Task ClearPendingCard(bool removeSwipeMarker = true, bool resetVisuals = false)
    {
        if (removeSwipeMarker)
        {
            await RemoveSwipeMarkerPower();
        }

        ClearPendingCardStateOnly();
        if (resetVisuals)
        {
            await CreatureCmd.TriggerAnim(Creature, "ClearSwallowed", 0f);
        }
    }

    private async Task RemoveSwipeMarkerPower()
    {
        SwipePower[] swipePowers = _pendingCards
            .Select(static pending => pending.SwipePower)
            .ToArray();
        foreach (SwipePower swipe in swipePowers)
        {
            swipe.StolenCard = null;
            if (Creature.Powers.Contains(swipe))
            {
                await PowerCmd.Remove(swipe);
            }
        }

        ClearStolenCardMarker();
    }

    private void ClearPendingCardStateOnly()
    {
        foreach (PendingStolenCard pending in _pendingCards)
        {
            pending.SwipePower.StolenCard = null;
        }

        _pendingCards.Clear();
        _waitingForPlayerWindow = false;
        _vulnerabilityWindowActive = false;
        _resistanceOverridden = false;
    }

    private void ShowLocalStolenCardMarker()
    {
        Marker2D? stolenCardPos = ResolveStolenCardMarker();
        if (stolenCardPos == null)
        {
            return;
        }

        stolenCardPos.FreeChildren();
        CardModel? card = _pendingCards
            .Select(static pending => pending.CombatCard)
            .FirstOrDefault(LocalContext.IsMine);
        if (card == null)
        {
            return;
        }

        NCard? nCard = NCard.Create(card);
        if (nCard == null)
        {
            return;
        }

        stolenCardPos.AddChildSafely(nCard);
        nCard.Position += nCard.Size * 0.5f;
        nCard.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
    }

    private void ClearStolenCardMarker()
    {
        ResolveStolenCardMarker()?.FreeChildren();
    }

    private Marker2D? ResolveStolenCardMarker()
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        return creatureNode?.Visuals.GetNodeOrNull<Marker2D>("%StolenCardPos");
    }

    private sealed record PendingStolenCard(
        CardModel CombatCard,
        CardModel DeckCard,
        Player Owner,
        SwipePower SwipePower);

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is not MoveState move)
            {
                continue;
            }

            foreach (AbstractIntent intent in move.Intents)
            {
                yield return intent;
            }
        }
    }
}

[HarmonyPatch(typeof(SwipePower), nameof(SwipePower.BeforeDeath))]
[LibraryPatch(Reason = "原版 SwipePower.BeforeDeath 会先于怪物自身把被吞的牌作为奖励归还，无 Hook；仅在拥有者是本模组大坏狼且死亡者就是拥有者时跳过，由狼自己的 BeforeDeath 归还。可改用自有能力，会新增模型 ID，留到阶段 6。")]
internal static class BigBadWolfSwipePowerRewardPatch
{
    private static bool Prefix(SwipePower __instance, Creature target, ref Task __result)
    {
        Creature? owner = __instance.Owner;
        if (owner?.Monster is not BigBadWolf)
        {
            return true;
        }

        if (owner != target)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
