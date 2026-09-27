using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.relics.StandaloneRelics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.events.AltarEnchantment;

public sealed class AncientMagicAltarEvent : EventModel
{
    private const string MaxHpSacrificeKey = "MaxHpSacrifice";
    private const int NormalPickCount = 1;
    private const int AdvancedPickCount = 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HpLossVar(0m),
        new DynamicVar(MaxHpSacrificeKey, 0m),
        new DynamicVar("NormalPickCount", NormalPickCount),
        new DynamicVar("AdvancedPickCount", AdvancedPickCount)
    ];

    public override bool IsAllowed(IRunState runState)
    {
        // if (runState is RunState concreteRunState)
        // {
        //     return !concreteRunState.VisitedEventIds.Contains(Id);
        // }
        //
        // return true;
        return false;
    }

    public override void CalculateVars()
    {
        
        int asc = Owner?.RunState?.AscensionLevel ?? 0;
        int ascBonus = Math.Min(9, asc / 3);
        DynamicVars.HpLoss.BaseValue = 9 + ascBonus;

        
        DynamicVars[MaxHpSacrificeKey].BaseValue = 14;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var owner = Owner;
        int normalTargets = owner != null
            ? AltarEnchantmentHelper.ListEnchantableDeckCards(owner, AltarEnchantmentHelper.NormalPool).Count
            : 0;
        int advTargets = owner != null
            ? AltarEnchantmentHelper.ListEnchantableDeckCards(owner, AltarEnchantmentHelper.AdvancedPool).Count
            : 0;

        var list = new List<EventOption>
        {
            normalTargets > 0
                ? new EventOption(this, AgreeLittle, InitialOptionKey("AGREE_LITTLE"))
                    .ThatDoesDamage(DynamicVars.HpLoss.BaseValue)
                : new EventOption(this, null, InitialOptionKey("AGREE_LITTLE_LOCKED")),
            advTargets >= AdvancedPickCount
                ? new EventOption(this, AgreeMuch, InitialOptionKey("AGREE_MUCH"))
                    .ThatDecreasesMaxHp(DynamicVars[MaxHpSacrificeKey].BaseValue)
                : new EventOption(this, null, InitialOptionKey("AGREE_MUCH_LOCKED")),
            new EventOption(this, RefuseAltar, InitialOptionKey("REFUSE"),
                HoverTipFactory.FromRelic<MagicCurseRelic>())
        };
        return list;
    }

    private async Task AgreeLittle()
    {
        if (Owner is not { } owner)
        {
            return;
        }

        await CreatureCmdCompat.Damage(
            new ThrowingPlayerChoiceContext(),
            owner.Creature,
            DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

        var prefsLittle = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, NormalPickCount);
        var pickedLittle = (await CardSelectCmd.FromDeckGeneric(
            owner,
            prefsLittle,
            c => c.Enchantment == null && AltarEnchantmentHelper.CanAnyEnchant(AltarEnchantmentHelper.NormalPool, c))).ToList();
        var card = pickedLittle.FirstOrDefault();
        if (card == null)
        {
            SetEventFinished(L10NLookup("ANCIENT_MAGIC_ALTAR_EVENT.pages.AGREE_LITTLE_NO_CARD.description"));
            return;
        }

        
        
        AltarEnchantmentHelper.EnchantRandomFromPool(Rng, card, AltarEnchantmentHelper.NormalPool);
        PlayEnchantVfx(card);

        SetEventFinished(L10NLookup("ANCIENT_MAGIC_ALTAR_EVENT.pages.AGREE_LITTLE.description"));
    }

    private async Task AgreeMuch()
    {
        if (Owner is not { } owner)
        {
            return;
        }

        await CreatureCmd.LoseMaxHp(
            new ThrowingPlayerChoiceContext(),
            owner.Creature,
            DynamicVars[MaxHpSacrificeKey].BaseValue,
            isFromCard: false);

        var prefs = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, AdvancedPickCount);
        var chosen = (await CardSelectCmd.FromDeckGeneric(
            owner,
            prefs,
            c => c.Enchantment == null && AltarEnchantmentHelper.CanAnyEnchant(AltarEnchantmentHelper.AdvancedPool, c))).ToList();

        foreach (var card in chosen)
        {
            if (card.Enchantment != null)
            {
                continue;
            }

            AltarEnchantmentHelper.EnchantRandomFromPool(Rng, card, AltarEnchantmentHelper.AdvancedPool);
            PlayEnchantVfx(card);
        }

        SetEventFinished(L10NLookup("ANCIENT_MAGIC_ALTAR_EVENT.pages.AGREE_MUCH.description"));
    }

    private async Task RefuseAltar()
    {
        if (Owner is not { } owner)
        {
            return;
        }

        await RelicCmd.Obtain<MagicCurseRelic>(owner);
        SetEventFinished(L10NLookup("ANCIENT_MAGIC_ALTAR_EVENT.pages.REFUSE.description"));
    }

    private static void PlayEnchantVfx(CardModel card)
    {
        var vfx = NCardEnchantVfx.Create(card);
        if (vfx != null)
        {
            NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
        }
    }
}
