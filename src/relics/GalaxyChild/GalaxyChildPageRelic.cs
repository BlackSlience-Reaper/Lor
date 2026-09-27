using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.GalaxyChild;
using LibraryOfRuina.enchantments.GalaxyChild;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.GalaxyChild;

public sealed class GalaxyChildPageRelic : RelicModel
{
    internal const int PebbleEnchantMaxSelect = 2;
    internal const int PebbleHeal = 3;
    internal const int ProofTurns = 3;
    internal const int ProofHeal = 4;
    internal const int TearsRemoveMaxSelect = 2;
    internal const int TearsPenaltyCombats = 2;
    internal const int TearsDrawReduction = 1;

    protected override string IconBaseName => "galaxy_child_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<GalaxyChildPageRelic>(runState);

    public override bool ShowCounter =>
        Mode switch
        {
            GalaxyChildPageMode.ProofOfFriendship =>
                CombatManager.Instance.IsInProgress && ProofTurnsRemainingThisCombat > 0,
            GalaxyChildPageMode.Tears => TearsPenaltyCombatsRemaining > 0,
            _ => false
        };

    public override int DisplayAmount =>
        Mode switch
        {
            GalaxyChildPageMode.ProofOfFriendship => ProofTurnsRemainingThisCombat,
            GalaxyChildPageMode.Tears => TearsPenaltyCombatsRemaining,
            _ => 0
        };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)GalaxyChildPageMode.None),
        new CardsVar(PebbleEnchantMaxSelect),
        new HealVar(PebbleHeal),
        new DynamicVar("ProofTurns", ProofTurns),
        new DynamicVar("ProofHeal", ProofHeal),
        new DynamicVar("RemoveCards", TearsRemoveMaxSelect),
        new DynamicVar("PenaltyCombats", TearsPenaltyCombats),
        new DynamicVar("DrawReduction", TearsDrawReduction)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        GalaxyChildPageMode.Pebble =>
        [
            ..HoverTipFactory.FromEnchantment<PebbleMarkEnchantment>(),
            HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
        ],
        _ => []
    };

    [SavedProperty]
    public GalaxyChildPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ProofTurnsRemainingThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TearsPenaltyCombatsRemaining { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != GalaxyChildPageMode.None)
        {
            UpdateModeUiState();
            RefreshInventoryIcon();
            return;
        }

        IReadOnlyList<CardModel> options = CreateModeChoiceCards();
        CardModel? chosenCard = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            options,
            Owner,
            canSkip: true);

        if (chosenCard == null)
        {
            await AbnormalityPageRewardHelper.SkipObtainedPageRelic(this, nameof(AfterObtained));
            return;
        }

        await SetMode(ResolveModeFromChoiceCard(chosenCard));
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        RefreshInventoryIcon();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetProofCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != Owner.Creature.Side
            || Mode != GalaxyChildPageMode.ProofOfFriendship
            || ProofTurnsRemainingThisCombat <= 0)
        {
            return;
        }

        IReadOnlyList<Creature> targets = combatState.Creatures
            .Where(static creature => creature.IsAlive)
            .OrderBy(static creature => creature.Side)
            .ThenBy(static creature => creature.CombatId ?? uint.MaxValue)
            .ToArray();
        if (targets.Count <= 0)
        {
            return;
        }

        Flash(targets);
        foreach (Creature target in targets)
        {
            await CreatureCmd.Heal(target, ProofHeal);
        }

        ProofTurnsRemainingThisCombat--;
        UpdateModeUiState();
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner
            || Mode != GalaxyChildPageMode.Tears
            || TearsPenaltyCombatsRemaining <= 0
            || player.Creature.CombatState?.RoundNumber != 1)
        {
            return count;
        }

        return Math.Max(0m, count - TearsDrawReduction);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetProofCombatState();
        if (Mode == GalaxyChildPageMode.Tears && TearsPenaltyCombatsRemaining > 0)
        {
            TearsPenaltyCombatsRemaining--;
        }
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<GalaxyChildPebbleChoiceCard>(Owner),
            Owner.RunState.CreateCard<GalaxyChildProofOfFriendshipChoiceCard>(Owner),
            Owner.RunState.CreateCard<GalaxyChildTearsChoiceCard>(Owner)
        ];
    }

    private static GalaxyChildPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            GalaxyChildPebbleChoiceCard => GalaxyChildPageMode.Pebble,
            GalaxyChildProofOfFriendshipChoiceCard => GalaxyChildPageMode.ProofOfFriendship,
            GalaxyChildTearsChoiceCard => GalaxyChildPageMode.Tears,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(GalaxyChildPageMode mode)
    {
        return mode is GalaxyChildPageMode.None
            or GalaxyChildPageMode.Pebble
            or GalaxyChildPageMode.ProofOfFriendship
            or GalaxyChildPageMode.Tears;
    }

    private static bool IsConcreteMode(GalaxyChildPageMode mode)
    {
        return mode is GalaxyChildPageMode.Pebble
            or GalaxyChildPageMode.ProofOfFriendship
            or GalaxyChildPageMode.Tears;
    }

    [AbnormalityPagePostObtainEffect]
    private async Task SetMode(GalaxyChildPageMode mode)
    {
        Mode = mode;
        ResetProofCombatState();
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();

        if (Mode == GalaxyChildPageMode.Pebble)
        {
            await ApplyPebbleEnchantmentSelection();
        }
        else if (Mode == GalaxyChildPageMode.Tears)
        {
            TearsPenaltyCombatsRemaining = TearsPenaltyCombats;
            await ApplyTearsRemovalSelection();
            UpdateModeUiState();
        }
    }

    private void EnsureValidModeOrFallback(string context)
    {
        if (IsConcreteMode(Mode))
        {
            return;
        }

        FallbackToDefaultModeAfterLoad(context);
    }

    private void FallbackToDefaultModeAfterLoad(string context)
    {
        GalaxyChildPageMode oldMode = Mode;
        Mode = GalaxyChildPageMode.Pebble;
        TearsPenaltyCombatsRemaining = 0;
        ResetProofCombatState();
        Log.Warn("[LibraryOfRuina.PageRelic] GalaxyChildPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Pebble.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void ResetProofCombatState()
    {
        ProofTurnsRemainingThisCombat = Mode == GalaxyChildPageMode.ProofOfFriendship
            ? ProofTurns
            : 0;
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            GalaxyChildPageMode.ProofOfFriendship when CombatManager.Instance.IsInProgress =>
                ProofTurnsRemainingThisCombat > 0 ? RelicStatus.Active : RelicStatus.Disabled,
            GalaxyChildPageMode.Tears =>
                TearsPenaltyCombatsRemaining > 0 ? RelicStatus.Active : RelicStatus.Disabled,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }

    private async Task ApplyPebbleEnchantmentSelection()
    {
        int targetCount = CountPebbleEnchantTargets(Owner);
        if (targetCount <= 0)
        {
            return;
        }

        EnchantmentModel enchantmentForSelection = ModelDb.Enchantment<PebbleMarkEnchantment>();
        CardSelectorPrefs prefs = new(
            SelectionScreenPrompt,
            minCount: 0,
            maxCount: Math.Min(PebbleEnchantMaxSelect, targetCount));

        IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckForEnchantment(
            Owner,
            enchantmentForSelection,
            amount: 1,
            prefs: prefs);

        foreach (CardModel card in selectedCards)
        {
            CardCmd.Enchant<PebbleMarkEnchantment>(card, 1);
            PlayEnchantVfx(card);
        }
    }

    private async Task ApplyTearsRemovalSelection()
    {
        int maxCount = Math.Min(TearsRemoveMaxSelect, PileType.Deck.GetPile(Owner).Cards.Count);
        if (maxCount <= 0)
        {
            return;
        }

        CardSelectorPrefs prefs = new(CardSelectorPrefs.RemoveSelectionPrompt, minCount: 0, maxCount: maxCount)
        {
            Cancelable = false,
            RequireManualConfirmation = true
        };
        IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckForRemoval(Owner, prefs);
        foreach (CardModel card in selectedCards)
        {
            await CardPileCmd.RemoveFromDeck(card);
        }
    }

    private static int CountPebbleEnchantTargets(Player owner)
    {
        if (owner == null)
        {
            return 0;
        }

        EnchantmentModel enchantment = ModelDb.Enchantment<PebbleMarkEnchantment>();
        return PileType.Deck.GetPile(owner).Cards.Count(enchantment.CanEnchant);
    }

    private static void PlayEnchantVfx(CardModel card)
    {
        NCardEnchantVfx? vfx = NCardEnchantVfx.Create(card);
        if (vfx != null)
        {
            NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
        }
    }

    private void RefreshInventoryIcon()
    {
        NRelicInventory? inventory = NRun.Instance?.GlobalUi?.RelicInventory;
        if (inventory == null)
        {
            return;
        }

        foreach (NRelicInventoryHolder holder in inventory.RelicNodes)
        {
            if (!ReferenceEquals(holder.Relic.Model, this))
            {
                continue;
            }

            holder.Relic.Icon.Texture = Icon;
            holder.Relic.Outline.Texture = IconOutline;
            break;
        }
    }
}
