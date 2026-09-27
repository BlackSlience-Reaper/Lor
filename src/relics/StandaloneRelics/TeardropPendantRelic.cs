using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.enchantments.Yanami;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.CardSelection;
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

namespace LibraryOfRuina.relics.StandaloneRelics;

public sealed class TeardropPendantRelic : YanamiRelicModel
{
    public const int MaxCardCount = 4;

    protected override string IconBaseName => "teardrop_pendant_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(MaxCardCount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..HoverTipFactory.FromEnchantment<YanamiEtherealEnchantment>()
    ];

    public static bool HasEnchantTargets(Player? player)
    {
        return CountEnchantableDeckCards(player) > 0;
    }

    public static int CountEnchantableDeckCards(Player? player)
    {
        if (player == null)
        {
            return 0;
        }

        EnchantmentModel enchantment = ModelDb.Enchantment<YanamiEtherealEnchantment>();
        return PileType.Deck.GetPile(player).Cards.Count(enchantment.CanEnchant);
    }

    public override async Task AfterObtained()
    {
        int targetCount = CountEnchantableDeckCards(Owner);
        if (targetCount <= 0)
        {
            return;
        }

        EnchantmentModel enchantmentForSelection = ModelDb.Enchantment<YanamiEtherealEnchantment>();
        CardSelectorPrefs prefs = new(
            CardSelectorPrefs.EnchantSelectionPrompt,
            minCount: 0,
            maxCount: Math.Min(MaxCardCount, targetCount));

        IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckForEnchantment(
            Owner,
            enchantmentForSelection,
            amount: 1,
            prefs: prefs);

        foreach (CardModel card in selectedCards)
        {
            CardCmd.Enchant<YanamiEtherealEnchantment>(card, 1);
            PlayEnchantVfx(card);
        }
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
