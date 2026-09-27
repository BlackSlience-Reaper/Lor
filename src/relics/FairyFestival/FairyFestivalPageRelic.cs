using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.FairyFestival;
using LibraryOfRuina.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.FairyFestival;

public sealed class FairyFestivalPageRelic : RelicModel
{
    internal const int FairyCareHeal = 6;
    internal const int FairyCarePermanentVulnerable = 1;
    internal const int GluttonyRegen = 2;
    internal const int PredationHpLoss = 3;
    internal const int PredationStrength = 2;
    internal const int PredationDexterity = 1;

    protected override string IconBaseName =>  "fairy_festival_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<FairyFestivalPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)FairyFestivalPageMode.None),
        new HealVar(FairyCareHeal),
        new DynamicVar("PermanentVulnerable", FairyCarePermanentVulnerable),
        new DynamicVar("Regen", GluttonyRegen),
        new HpLossVar(PredationHpLoss),
        new PowerVar<StrengthPower>(PredationStrength),
        new PowerVar<DexterityPower>(PredationDexterity)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<RegenPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    [SavedProperty]
    public FairyFestivalPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool GluttonySpent { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        UpdateModeUiState();
        if (Mode != FairyFestivalPageMode.None)
        {
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

        Mode = ResolveModeFromChoiceCard(chosenCard);
        UpdateModeUiState();
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        GluttonySpent = false;
        Creature? ownerCreature = Owner?.Creature;
        if (ownerCreature?.CombatState is not { } combatState)
        {
            return;
        }

        if (Mode == FairyFestivalPageMode.FairyCare)
        {
            Flash();
            await CreatureCmd.Heal(ownerCreature, FairyCareHeal);
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                ownerCreature,
                FairyCarePermanentVulnerable,
                turns: -1,
                ownerCreature,
                null,
                silent: true);
        }
        else if (Mode == FairyFestivalPageMode.Predation)
        {
            Flash();
            foreach (Creature creature in combatState.Creatures.Where(static creature => creature.IsAlive).ToList())
            {
                await CreatureCmdCompat.Damage(
                    new BlockingPlayerChoiceContext(),
                    creature,
                    PredationHpLoss,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    ownerCreature,
                    null);
            }

            await PowerCmdCompat.Apply<StrengthPower>(ownerCreature, PredationStrength, ownerCreature, null);
            await PowerCmdCompat.Apply<DexterityPower>(ownerCreature, PredationDexterity, ownerCreature, null);
        }

        UpdateModeUiState();
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode != FairyFestivalPageMode.Gluttony
            || GluttonySpent
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props)
            || dealer != Owner.Creature
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target))
        {
            return;
        }

        GluttonySpent = true;
        Flash([target]);
        await PowerCmdCompat.Apply<RegenPower>(Owner.Creature, GluttonyRegen, Owner.Creature, cardSource);
        UpdateModeUiState();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        GluttonySpent = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<FairyCareChoiceCard>(Owner),
            Owner.RunState.CreateCard<FairyGluttonyChoiceCard>(Owner),
            Owner.RunState.CreateCard<FairyPredationChoiceCard>(Owner)
        ];
    }

    private static FairyFestivalPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            FairyCareChoiceCard => FairyFestivalPageMode.FairyCare,
            FairyGluttonyChoiceCard => FairyFestivalPageMode.Gluttony,
            FairyPredationChoiceCard => FairyFestivalPageMode.Predation,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(FairyFestivalPageMode mode)
    {
        return mode is FairyFestivalPageMode.None
            or FairyFestivalPageMode.FairyCare
            or FairyFestivalPageMode.Gluttony
            or FairyFestivalPageMode.Predation;
    }

    private static bool IsConcreteMode(FairyFestivalPageMode mode)
    {
        return mode is FairyFestivalPageMode.FairyCare
            or FairyFestivalPageMode.Gluttony
            or FairyFestivalPageMode.Predation;
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
        FairyFestivalPageMode oldMode = Mode;
        Mode = FairyFestivalPageMode.FairyCare;
        GluttonySpent = false;
        Log.Warn("[LibraryOfRuina.PageRelic] FairyFestivalPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to FairyCare.");
        UpdateModeUiState();
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == FairyFestivalPageMode.Gluttony && CombatManager.Instance.IsInProgress
            ? GluttonySpent ? RelicStatus.Disabled : RelicStatus.Active
            : RelicStatus.Normal;
    }
}
