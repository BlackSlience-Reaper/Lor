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
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.GalaxyChild;

public sealed class GalaxyChildPageRelic : ModalPageRelic<GalaxyChildPageMode>
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

    protected override GalaxyChildPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ProofTurnsRemainingThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TearsPenaltyCombatsRemaining { get; private set; }

    protected override Task ApplyObtainedChoiceAsync(GalaxyChildPageMode mode) => SetModeAsync(mode);

    // 预选只写模式、通知图标变化并刷新界面；清状态与拾取效果由获得后作为 AbnormalityPagePostObtainEffect 执行的 SetModeAsync 完成。
    protected override void ApplyPreselectedMode(GalaxyChildPageMode mode) => AssignPreselectedModeOnly(mode);

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

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<GalaxyChildPebbleChoiceCard>(Owner),
            Owner.RunState.CreateCard<GalaxyChildProofOfFriendshipChoiceCard>(Owner),
            Owner.RunState.CreateCard<GalaxyChildTearsChoiceCard>(Owner)
        ];
    }

    [AbnormalityPagePostObtainEffect]
    private async Task SetModeAsync(GalaxyChildPageMode mode)
    {
        SetMode(mode);

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

    private void ResetProofCombatState()
    {
        ProofTurnsRemainingThisCombat = Mode == GalaxyChildPageMode.ProofOfFriendship
            ? ProofTurns
            : 0;
    }

    protected override void ResetStateOnModeSet(GalaxyChildPageMode mode) => ResetProofCombatState();

    protected override void ResetStateOnFallback()
    {
        TearsPenaltyCombatsRemaining = 0;
        ResetProofCombatState();
    }

    protected override void UpdateModeUiState()
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
}
