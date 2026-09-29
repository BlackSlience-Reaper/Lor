using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters.PriceOfSilence;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.powers.PriceOfSilence;
using LibraryOfRuina.relics.PriceOfSilence;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.PriceOfSilence;

[CardPool(typeof(StatusCardPool))]
public sealed class SilenceStatusCard : CardModel
{
    public const int HpLoss = 12;

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/status/silence_status_card.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HpLossVar(HpLoss),
        new DynamicVar("Turns", 1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromPower<PriceOfSilenceSilencePower>()
    ];

    public SilenceStatusCard()
        : base(1, CardType.Status, CardRarity.Status, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? boss = PriceOfSilenceEncounterHelper.FindBoss(Owner.Creature.CombatState);
        PriceOfSilenceEncounterHelper.FindTracker(Owner.Creature.CombatState)?.MarkSilenceUsed(Owner);
        Owner.Creature.GetPower<TimeTraceMarkedPlayerPower>()?.MarkSilenceUsed();
        if (boss != null)
        {
            await PowerCmdCompat.Apply<PriceOfSilenceSilencePower>(
                choiceContext,
                Owner.Creature,
                1,
                boss,
                this);
        }

        LocalOggOneShotPlayer.Play(monsters.PriceOfSilence.PriceOfSilence.SilenceCardSfxPath, -2f);
        await CreatureCmdCompat.Damage(
            choiceContext,
            Owner.Creature,
            DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature,
            this,
            cardPlay);
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is SilenceStatusCard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}

public abstract class PriceOfSilencePageChoiceCardBase : PageChoiceCard<PriceOfSilencePageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromPower<LibraryWeakPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(PriceOfSilencePageRelic.TimeEnergy),
        new DynamicVar("Cards", PriceOfSilencePageRelic.TimeDraw),
        new DynamicVar("CardLimit", PriceOfSilencePageRelic.TimeCardLimit),
        new DynamicVar("EnergyLoss", PriceOfSilencePageRelic.TimeEnergyLoss),
        new DynamicVar("Thirteenth", PriceOfSilencePageRelic.ThirteenthCard),
        new DynamicVar("Draw", PriceOfSilencePageRelic.ThirteenthDraw),
        new DynamicVar("Weak", PriceOfSilencePageRelic.SilenceWeak),
        new DynamicVar("Turns", PriceOfSilencePageRelic.SilenceWeakTurns),
        new DynamicVar("Cooldown", PriceOfSilencePageRelic.SilenceCooldown)
    ];
}

[CardPool(typeof(TokenCardPool))]
public sealed class PriceOfSilenceTimeChoiceCard : PriceOfSilencePageChoiceCardBase
{
    public override PriceOfSilencePageMode PageMode => PriceOfSilencePageMode.Time;

    protected override string PortraitFileName => "price_of_silence_time_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class PriceOfSilenceThirteenthTollChoiceCard : PriceOfSilencePageChoiceCardBase
{
    public override PriceOfSilencePageMode PageMode => PriceOfSilencePageMode.ThirteenthToll;

    protected override string PortraitFileName => "price_of_silence_thirteenth_toll_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class PriceOfSilenceSilenceChoiceCard : PriceOfSilencePageChoiceCardBase
{
    public override PriceOfSilencePageMode PageMode => PriceOfSilencePageMode.Silence;

    protected override string PortraitFileName => "price_of_silence_silence_choice_card.png";
}
