using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.cards.Leticia;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.enchantments.Leticia;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.Leticia;

public sealed class LeticiaPageRelic : ModalPageRelic<LeticiaPageMode>
{
    internal const int SurpriseGiftChoiceCount = 3;
    internal const int SurpriseGiftPickCount = 1;
    internal const int BuddyEnchantMaxSelect = 1;
    internal const int PrankStatAmount = 3;
    internal const int PrankTurnInterval = 3;
    internal const int PrankDebuffDuration = 1;

    private static readonly string LeticiaPageRelicTitleLocKey =
        $"{ModelDb.GetId<LeticiaPageRelic>().Entry}.title";

    private bool _mischiefTriggerTurn;

    protected override string IconBaseName => "leticia_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<LeticiaPageRelic>(runState);

    public override bool ShowCounter => Mode == LeticiaPageMode.Mischief;

    public override int DisplayAmount =>
        Mode == LeticiaPageMode.Mischief ? GetMischiefDisplayAmount() : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)LeticiaPageMode.None),
        new DynamicVar("ChoiceCount", SurpriseGiftChoiceCount),
        new DynamicVar("PickCount", SurpriseGiftPickCount),
        new CardsVar(BuddyEnchantMaxSelect),
        new PowerVar<StrengthPower>(PrankStatAmount),
        new PowerVar<DexterityPower>(PrankStatAmount),
        new DynamicVar("TurnInterval", PrankTurnInterval),
        new DynamicVar("Turns", PrankDebuffDuration),
        new DynamicVar("CostReduction", LeticiaPartnerMarkEnchantment.PlayCostReduction),
        new DynamicVar("CostIncrease", LeticiaPartnerMarkEnchantment.HandEndCostIncrease)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<FrailPower>(),
        ..HoverTipFactory.FromEnchantment<LeticiaPartnerMarkEnchantment>()
    ];

    [SavedProperty]
    public LeticiaPageMode Mode { get; private set; }

    protected override LeticiaPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshUiBeforeModeChoice => true;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool SurpriseGiftTriggeredThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MischiefTurnsSeen { get; private set; }

    protected override async Task ApplyObtainedChoiceAsync(LeticiaPageMode mode)
    {
        SetMode(mode);
        if (Mode == LeticiaPageMode.Buddy)
        {
            await ApplyBuddyEnchantmentSelection();
        }
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        SurpriseGiftTriggeredThisCombat = false;
        _mischiefTriggerTurn = false;
        UpdateModeUiState();

        if (Mode == LeticiaPageMode.Mischief)
        {
            Flash();
            await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, PrankStatAmount, Owner.Creature, null);
            await PowerCmdCompat.Apply<DexterityPower>(Owner.Creature, PrankStatAmount, Owner.Creature, null);
        }

        UpdateModeUiState();
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatStateLike combatState)
    {
        EnsureValidModeOrFallback(nameof(BeforeHandDraw));

        if (Mode != LeticiaPageMode.SurpriseGift
            || player != Owner
            || combatState.RoundNumber != 1
            || SurpriseGiftTriggeredThisCombat)
        {
            return;
        }

        SurpriseGiftTriggeredThisCombat = true;
        await ResolveSurpriseGiftCombatStart(choiceContext, player);
        UpdateModeUiState();
    }

    /// <summary>
    /// Mischief prank cadence. Runs in <c>BeforeSideTurnStart</c> instead of
    /// <c>AfterPlayerTurnStart</c> on purpose: the game dispatches
    /// <c>Hook.AfterPlayerTurnStart</c> with plain awaits, so a player-choice block in any
    /// earlier model suspends the dispatch past the "After player turn start" checksum on the
    /// machine waiting for the remote choice. The accumulated <c>MischiefTurnsSeen</c> counter
    /// then differs at snapshot time and desyncs the session (observed in multiplayer logs:
    /// client 2 vs host 1). <c>Hook.BeforeSideTurnStart</c> runs before the checksum and uses
    /// per-model pause-or-completion, so this update always completes before the snapshot on
    /// every machine.
    /// </summary>
    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (Owner.Creature == null
            || Mode != LeticiaPageMode.Mischief
            || side != Owner.Creature.Side
            || !Owner.Creature.IsAlive
            || !participants.Contains(Owner.Creature))
        {
            UpdateModeUiState();
            return;
        }

        _mischiefTriggerTurn = MischiefTurnsSeen >= PrankTurnInterval - 1;
        MischiefTurnsSeen = _mischiefTriggerTurn ? 0 : MischiefTurnsSeen + 1;
        UpdateModeUiState();

        if (!_mischiefTriggerTurn)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.ApplyDebuff<WeakPower>(Owner.Creature, PrankDebuffDuration, Owner.Creature, null);
        await PowerCmdCompat.ApplyDebuff<FrailPower>(Owner.Creature, PrankDebuffDuration, Owner.Creature, null);

        UpdateModeUiState();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _mischiefTriggerTurn = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<LeticiaPageSurpriseGiftChoiceCard>(Owner),
            Owner.RunState.CreateCard<LeticiaPageBuddyChoiceCard>(Owner),
            Owner.RunState.CreateCard<LeticiaPageMischiefChoiceCard>(Owner)
        ];
    }

    // 联机时不在本地改写已同步的模式，只记日志。
    protected override void FallbackToDefaultModeAfterLoad(string context)
    {
        LeticiaPageMode oldMode = Mode;
        bool canMutateLocally;
        try
        {
            canMutateLocally = RunManager.Instance.IsSingleplayerOrFakeMultiplayer;
        }
        catch
        {
            canMutateLocally = true;
        }

        if (canMutateLocally)
        {
            Mode = LeticiaPageMode.SurpriseGift;
        }

        Log.Warn("[LibraryOfRuina.PageRelic] LeticiaPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + (canMutateLocally
                ? "; fallback to SurpriseGift."
                : "; kept Mode unchanged in multiplayer (no local mutation of replicated state)."));
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == LeticiaPageMode.Mischief
            && CombatManager.Instance.IsInProgress
            && _mischiefTriggerTurn
                ? RelicStatus.Active
                : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private int GetMischiefDisplayAmount() =>
        _mischiefTriggerTurn ? PrankTurnInterval : MischiefTurnsSeen;

    [AbnormalityPagePostObtainEffect((int)LeticiaPageMode.Buddy)]
    private async Task ApplyBuddyEnchantmentSelection()
    {
        EnchantmentModel enchantmentForSelection = ModelDb.Enchantment<LeticiaPartnerMarkEnchantment>();
        CardSelectorPrefs prefs = new(SelectionScreenPrompt, BuddyEnchantMaxSelect);

        IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckForEnchantment(
            Owner,
            enchantmentForSelection,
            amount: 1,
            additionalFilter: static card => card?.Type == CardType.Skill,
            prefs: prefs);

        foreach (CardModel card in selectedCards)
        {
            // Re-validate right before applying: the selection runs through a
            // multiplayer choice round-trip, and cross-mod cards (or cards whose
            // state changed in between) can stop being enchantable after the pick.
            // CardCmd.Enchant throws InvalidOperationException in that case, which
            // would fault the relic obtain task; skip those cards instead.
            if (!enchantmentForSelection.CanEnchant(card))
            {
                Log.Warn("[LibraryOfRuina.PageRelic] LeticiaPageRelic Buddy enchant skipped: card "
                    + card.Id
                    + " is no longer enchantable with "
                    + enchantmentForSelection.Id
                    + "; relic effect left as-is for this card.");
                continue;
            }

            CardCmd.Enchant<LeticiaPartnerMarkEnchantment>(card, 1);
            PlayEnchantVfx(card);
        }
    }

    private async Task ResolveSurpriseGiftCombatStart(PlayerChoiceContext choiceContext, Player player)
    {
        IEnumerable<CardModel> characterCards = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(static c => c.Rarity == CardRarity.Rare && c.IsUpgradable);

        List<CardModel> options = CardFactory.GetDistinctForCombat(
                player,
                characterCards,
                SurpriseGiftChoiceCount,
                player.RunState.Rng.CombatCardGeneration)
            .ToList();

        foreach (CardModel card in options)
        {
            CardCmd.Upgrade(card);
        }

        if (options.Count == 0)
        {
            Flash();
            Log.Info("[LibraryOfRuina.PageRelic] LeticiaPageRelic SurpriseGift: no upgradable cards in character card pool.");
            return;
        }

        Flash();
        CardModel? chosen;
        try
        {
            chosen = await ChooseSurpriseGiftCardOnMainThread(choiceContext, options, player);
        }
        catch (Exception exception) when (ShouldSuppressChooseCardOverlayLifecycleException(exception))
        {
            Log.Warn("[LibraryOfRuina.PageRelic] LeticiaPageRelic SurpriseGift choice overlay failed at combat start; "
                + "skipping this combat. exception="
                + exception.GetType().Name
                + ": "
                + exception.Message);
            return;
        }

        if (chosen == null)
        {
            return;
        }

        chosen.SetToFreeThisCombat();
        await CardPileCmdCompat.AddGeneratedCardToCombat(chosen, PileType.Hand, addedByPlayer: true);
    }

    private static Task<CardModel?> ChooseSurpriseGiftCardOnMainThread(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<CardModel> options,
        Player player)
    {
        if (NGame.IsMainThread())
        {
            return ChooseSurpriseGiftCardCore(choiceContext, options, player);
        }

        TaskCompletionSource<CardModel?> completionSource = new();
        Callable.From(async () =>
        {
            try
            {
                completionSource.SetResult(await ChooseSurpriseGiftCardCore(choiceContext, options, player));
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        }).CallDeferred();
        return completionSource.Task;
    }

    private static Task<CardModel?> ChooseSurpriseGiftCardCore(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<CardModel> options,
        Player player)
    {
        return CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            options,
            player,
            canSkip: true);
    }

    private static bool ShouldSuppressChooseCardOverlayLifecycleException(Exception exception)
    {
        if (exception is not NullReferenceException && exception is not ObjectDisposedException)
        {
            return false;
        }

        string stackTrace = exception.StackTrace ?? string.Empty;
        return stackTrace.Contains(
                "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen.AfterOverlayShown",
                StringComparison.Ordinal)
            || stackTrace.Contains(
                "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen.AfterOverlayHidden",
                StringComparison.Ordinal);
    }

    private static void PlayEnchantVfx(CardModel card)
    {
        NCardEnchantVfx? vfx = NCardEnchantVfx.Create(card);
        if (vfx != null)
        {
            NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
        }
    }
}
