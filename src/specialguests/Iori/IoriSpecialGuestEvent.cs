using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.enchantments.Iori;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.specialguests.Iori;

public sealed class IoriSpecialGuestEvent : SpecialGuestEventBase
{
    private const string EnchantedResolution = "enchanted_attack";
    private const string SnuckAwayResolution = "snuck_away";
    private const string OptionRoot =
        "IORI_SPECIAL_GUEST_EVENT.pages.INITIAL.options.";

    protected override string GuestDefinitionId => IoriSpecialGuestIds.Guest;

    protected override string SecondOptionLocKey =>
        OptionRoot + "ENCHANT_ATTACK";

    protected override string ThirdOptionLocKey =>
        OptionRoot + "REMOVE_CARD";

    protected override EventOption CreateCustomSecondOption()
    {
        IoriUnpredictableEnchantment enchantment =
            ModelDb.Enchantment<IoriUnpredictableEnchantment>();
        bool everyPlayerHasEligibleCard = Owner?.RunState is IRunState runState
            && runState.Players.All(player =>
                PileType.Deck.GetPile(player).Cards.Any(enchantment.CanEnchant));

        return CreateOption(
            everyPlayerHasEligibleCard
                ? SecondOptionLocKey
                : OptionRoot + "ENCHANT_ATTACK_LOCKED",
            everyPlayerHasEligibleCard ? EnchantAttack : null,
            HoverTipFactory.FromEnchantment<IoriUnpredictableEnchantment>());
    }

    protected override EventOption CreateCustomThirdOption() =>
        CreateOption(ThirdOptionLocKey, RemoveCard);

    protected override LocString? ResolveCustomResolutionDescription(
        string resolution) => resolution switch
    {
        EnchantedResolution => new LocString(
            "events",
            "IORI_SPECIAL_GUEST_EVENT.pages.ENCHANTED.description"),
        SnuckAwayResolution => new LocString(
            "events",
            "IORI_SPECIAL_GUEST_EVENT.pages.SNUCK_AWAY.description"),
        _ => null,
    };

    private async Task EnchantAttack()
    {
        if (Owner is { } owner)
        {
            CardModel? selected = (await CardSelectCmd.FromDeckForEnchantment(
                    owner,
                    ModelDb.Enchantment<IoriUnpredictableEnchantment>(),
                    1,
                    new CardSelectorPrefs(
                        CardSelectorPrefs.EnchantSelectionPrompt,
                        1)))
                .FirstOrDefault();
            if (selected != null)
            {
                CardCmd.Enchant<IoriUnpredictableEnchantment>(selected, 1m);
                NCardEnchantVfx? vfx = NCardEnchantVfx.Create(selected);
                if (vfx != null)
                {
                    NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
                }
            }
        }

        await FinishWithoutReceptionAsync(
            EnchantedResolution,
            new LocString(
                "events",
                "IORI_SPECIAL_GUEST_EVENT.pages.ENCHANTED.description"));
    }

    private async Task RemoveCard()
    {
        if (Owner is { } owner)
        {
            CardModel? selected = (await CardSelectCmd.FromDeckForRemoval(
                    owner,
                    new CardSelectorPrefs(
                        CardSelectorPrefs.RemoveSelectionPrompt,
                        1)))
                .FirstOrDefault();
            if (selected != null)
            {
                await CardPileCmd.RemoveFromDeck(selected);
            }
        }

        await FinishWithoutReceptionAsync(
            SnuckAwayResolution,
            new LocString(
                "events",
                "IORI_SPECIAL_GUEST_EVENT.pages.SNUCK_AWAY.description"));
    }
}
