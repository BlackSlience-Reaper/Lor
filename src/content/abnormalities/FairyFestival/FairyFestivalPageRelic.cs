using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

public sealed class FairyFestivalPageRelic : ModalPageRelic<FairyFestivalPageMode>
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

    protected override FairyFestivalPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshIconOnModeChange => false;

    protected override bool RefreshUiBeforeModeChoice => true;

    // 预选写入模式时还会通知一次图标变化，获得时选择则不会。
    protected override void ApplyPreselectedMode(FairyFestivalPageMode mode) => AssignPreselectedModeOnly(mode);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool GluttonySpent { get; private set; }

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
            foreach (Creature creature in combatState.LivingCreatures().ToList())
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

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<FairyCareChoiceCard>(Owner),
            Owner.RunState.CreateCard<FairyGluttonyChoiceCard>(Owner),
            Owner.RunState.CreateCard<FairyPredationChoiceCard>(Owner)
        ];
    }

    protected override void ResetStateOnFallback() => GluttonySpent = false;

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == FairyFestivalPageMode.Gluttony && CombatManager.Instance.IsInProgress
            ? GluttonySpent ? RelicStatus.Disabled : RelicStatus.Active
            : RelicStatus.Normal;
    }
}
