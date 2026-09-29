using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.relics.StandaloneRelics;

public sealed class ProofOfExistenceRelic : RelicModel
{
    private enum BlessingType
    {
        Block,
        Strength,
        Dexterity,
        FirstDrawEnchantment
    }

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "fox_companion_relic";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Unpowered),
        new PowerVar<StrengthPower>(1m),
        new PowerVar<DexterityPower>(1m),
        new DynamicVar("SwiftAmount", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        ..HoverTipFactory.FromEnchantment<Swift>(DynamicVars["SwiftAmount"].IntValue)
    ];

    [SavedProperty]
    public bool PendingFirstDrawEnchantment { get; set; }

    private CardModel? _swiftFallbackCard;

    private bool _swiftFallbackUsed;

    public override async Task BeforeCombatStart()
    {
        PendingFirstDrawEnchantment = false;
        _swiftFallbackCard = null;
        _swiftFallbackUsed = false;
        Flash();

        BlessingType blessing = (BlessingType)Owner.RunState.Rng.Niche.NextInt(4);
        switch (blessing)
        {
            case BlessingType.Block:
                await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
                break;
            case BlessingType.Strength:
                await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
                break;
            case BlessingType.Dexterity:
                await PowerCmdCompat.Apply<DexterityPower>(Owner.Creature, DynamicVars.Dexterity.BaseValue, Owner.Creature, null);
                break;
            case BlessingType.FirstDrawEnchantment:
                PendingFirstDrawEnchantment = true;
                break;
        }
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (!PendingFirstDrawEnchantment) return Task.CompletedTask;
        if (card.Owner != Owner) return Task.CompletedTask;

        PendingFirstDrawEnchantment = false;
        ApplyBlessingToCard(card);
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (_swiftFallbackUsed || _swiftFallbackCard == null) return Task.CompletedTask;
        if (cardPlay.Card != _swiftFallbackCard) return Task.CompletedTask;

        _swiftFallbackUsed = true;
        _swiftFallbackCard = null;
        return CardPileCmd.Draw(context, DynamicVars["SwiftAmount"].BaseValue, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        PendingFirstDrawEnchantment = false;
        _swiftFallbackCard = null;
        _swiftFallbackUsed = false;
        return Task.CompletedTask;
    }

    private void ApplyBlessingToCard(CardModel card)
    {
        if (ModelDb.Enchantment<Swift>().CanEnchant(card))
        {
            CardCmd.Enchant<Swift>(card, DynamicVars["SwiftAmount"].BaseValue);
            return;
        }

        _swiftFallbackCard = card;
        _swiftFallbackUsed = false;
    }
}
