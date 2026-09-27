using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.TodaysShyLook;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.TodaysShyLook;

public sealed class TodaysShyLookPageRelic : RelicModel
{
    internal const int TodaysExpressionStrengthMin = -1;
    internal const int TodaysExpressionStrengthMax = 3;
    internal const int TodaysExpressionDexterityMin = -1;
    internal const int TodaysExpressionDexterityMax = 1;
    internal const int TodaysExpressionDexterityFloor = 0;
    internal const int ShynessBlock = 3;
    internal const int SocialDistanceBlockPerSkill = 1;
    internal const int SocialDistanceMaxBlock = 3;

    protected override string IconBaseName => "todays_shy_look_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<TodaysShyLookPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)TodaysShyLookPageMode.None),
        new DynamicVar("StrengthMin", TodaysExpressionStrengthMin),
        new DynamicVar("StrengthMax", TodaysExpressionStrengthMax),
        new DynamicVar("DexterityMin", TodaysExpressionDexterityMin),
        new DynamicVar("DexterityMax", TodaysExpressionDexterityMax),
        new DynamicVar("DexterityFloor", TodaysExpressionDexterityFloor),
        new BlockVar(ShynessBlock, ValueProp.Unpowered),
        new DynamicVar("BlockPerSkill", SocialDistanceBlockPerSkill),
        new DynamicVar("MaxBlock", SocialDistanceMaxBlock)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        TodaysShyLookPageMode.TodaysExpression =>
        [
            HoverTipFactory.FromPower<StrengthPower>(),
            HoverTipFactory.FromPower<DexterityPower>()
        ],
        TodaysShyLookPageMode.Shyness =>
        [
            HoverTipFactory.Static(StaticHoverTip.Block)
        ],
        TodaysShyLookPageMode.SocialDistance =>
        [
            HoverTipFactory.Static(StaticHoverTip.Block)
        ],
        _ => []
    };

    [SavedProperty]
    public TodaysShyLookPageMode Mode { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != TodaysShyLookPageMode.None)
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

        SetMode(ResolveModeFromChoiceCard(chosenCard));
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        RefreshInventoryIcon();
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        UpdateModeUiState();

        switch (Mode)
        {
            case TodaysShyLookPageMode.TodaysExpression:
                await ApplyTodaysExpressionStartEffects();
                break;
        }
    }

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (Mode != TodaysShyLookPageMode.SocialDistance
            || target != Owner.Creature
            || cardSource?.Owner != Owner)
        {
            return 0m;
        }

        int skillCards = PileType.Hand.GetPile(Owner).Cards.Count(static card => card.Type == CardType.Skill);
        return Math.Min(SocialDistanceMaxBlock, skillCards * SocialDistanceBlockPerSkill);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Mode != TodaysShyLookPageMode.Shyness || side != Owner.Creature.Side)
        {
            return;
        }

        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, ShynessBlock, ValueProp.Unpowered, null, fast: true);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private async Task ApplyTodaysExpressionStartEffects()
    {
        int strengthDelta = Owner.RunState.Rng.Niche.NextInt(TodaysExpressionStrengthMin, TodaysExpressionStrengthMax + 1);
        int rawDexterityDelta = Owner.RunState.Rng.Niche.NextInt(TodaysExpressionDexterityMin, TodaysExpressionDexterityMax + 1);
        int dexterityDelta = rawDexterityDelta < TodaysExpressionDexterityFloor ? TodaysExpressionDexterityFloor : rawDexterityDelta;

        if (strengthDelta != 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, strengthDelta, Owner.Creature, null);
        }

        if (dexterityDelta != 0)
        {
            await PowerCmdCompat.Apply<DexterityPower>(Owner.Creature, dexterityDelta, Owner.Creature, null);
        }

        if (strengthDelta != 0 || dexterityDelta != 0)
        {
            Flash();
        }
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<TodaysShyLookTodaysExpressionChoiceCard>(Owner),
            Owner.RunState.CreateCard<TodaysShyLookShynessChoiceCard>(Owner),
            Owner.RunState.CreateCard<TodaysShyLookSocialDistanceChoiceCard>(Owner)
        ];
    }

    private static TodaysShyLookPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            TodaysShyLookTodaysExpressionChoiceCard => TodaysShyLookPageMode.TodaysExpression,
            TodaysShyLookShynessChoiceCard => TodaysShyLookPageMode.Shyness,
            TodaysShyLookSocialDistanceChoiceCard => TodaysShyLookPageMode.SocialDistance,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(TodaysShyLookPageMode mode)
    {
        return mode is TodaysShyLookPageMode.None
            or TodaysShyLookPageMode.TodaysExpression
            or TodaysShyLookPageMode.Shyness
            or TodaysShyLookPageMode.SocialDistance;
    }

    private static bool IsConcreteMode(TodaysShyLookPageMode mode)
    {
        return mode is TodaysShyLookPageMode.TodaysExpression
            or TodaysShyLookPageMode.Shyness
            or TodaysShyLookPageMode.SocialDistance;
    }

    private void SetMode(TodaysShyLookPageMode mode)
    {
        Mode = mode;
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
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
        TodaysShyLookPageMode oldMode = Mode;
        Mode = TodaysShyLookPageMode.TodaysExpression;
        Log.Warn("[LibraryOfRuina.PageRelic] TodaysShyLookPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to TodaysExpression.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
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
