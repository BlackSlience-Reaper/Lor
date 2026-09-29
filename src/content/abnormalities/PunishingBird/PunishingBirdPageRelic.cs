using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
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

namespace LibraryOfRuina.content.abnormalities.PunishingBird;

public sealed class PunishingBirdPageRelic : LibraryRelicModel
{
    public const int PunishmentMultiplier = 9;
    public const int BeakStrength = 5;
    public const int BeakDexterity = 2;
    public const int WingsEnergyGain = 1;

    protected override string IconBaseName => "punishing_bird_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<PunishingBirdPageRelic>(runState);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.ForEnergy(this)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)PunishingBirdPageMode.None),
        new DynamicVar("Multiplier", PunishmentMultiplier),
        new DynamicVar("Strength", BeakStrength),
        new DynamicVar("Dexterity", BeakDexterity),
        new EnergyVar("EnergyLoss", WingsEnergyGain)
    ];

    [SavedProperty]
    public PunishingBirdPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HpLossSinceLastOwnerTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HpLostDuringCurrentRound { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BeakStrengthGranted { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BeakDexterityGranted { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int WingsTemporaryStrength { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int WingsTemporaryDexterity { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultMode(nameof(AfterObtained));
            return;
        }

        if (Mode != PunishingBirdPageMode.None)
        {
            UpdateModeUi();
            return;
        }

        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            CreateChoiceCards(),
            Owner,
            canSkip: true);
        if (chosen == null)
        {
            await AbnormalityPageRewardHelper.SkipObtainedPageRelic(this, nameof(AfterObtained));
            return;
        }

        Mode = chosen switch
        {
            PunishingBirdPunishmentChoiceCard => PunishingBirdPageMode.Punishment,
            PunishingBirdPunitiveBeakChoiceCard => PunishingBirdPageMode.PunitiveBeak,
            PunishingBirdFlutteringWingsChoiceCard => PunishingBirdPageMode.FlutteringWings,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(chosen)
        };
        UpdateModeUi();
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidMode(nameof(AfterRoomEntered));
        UpdateModeUi();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidMode(nameof(BeforeCombatStart));
        HpLossSinceLastOwnerTurn = 0;
        HpLostDuringCurrentRound = false;
        BeakStrengthGranted = 0;
        BeakDexterityGranted = 0;
        WingsTemporaryStrength = 0;
        WingsTemporaryDexterity = 0;
        UpdateModeUi();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Owner.Creature.CombatState == null)
        {
            return;
        }

        if (Mode == PunishingBirdPageMode.Punishment
            && Owner.Creature.CombatState.RoundNumber >= 2
            && HpLossSinceLastOwnerTurn > 0)
        {
            Flash();
            int damage = HpLossSinceLastOwnerTurn * PunishmentMultiplier;
            Creature[] enemies = AllyTurnRegistry
                .FilterPlayerEnemyTargets(Owner.Creature.CombatState.Enemies)
                .Where(static enemy => enemy.IsAlive)
                .ToArray();
            if (enemies.Length > 0)
            {
                await CreatureCmdCompat.Damage(
                    choiceContext,
                    enemies,
                    damage,
                    ValueProp.Unpowered,
                    Owner.Creature,
                    null);
            }
        }

        HpLossSinceLastOwnerTurn = 0;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner.Creature || result.UnblockedDamage <= 0)
        {
            return;
        }

        HpLossSinceLastOwnerTurn += result.UnblockedDamage;
        HpLostDuringCurrentRound = true;
        if (Mode == PunishingBirdPageMode.PunitiveBeak)
        {
            await ResetBeakBonuses();
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Mode != PunishingBirdPageMode.FlutteringWings || cardPlay.Card.Owner != Owner)
        {
            return;
        }

        switch (cardPlay.Card.Type)
        {
            case CardType.Attack:
                WingsTemporaryDexterity++;
                await PowerCmdCompat.Apply<DexterityPower>(Owner.Creature, 1, Owner.Creature, cardPlay.Card);
                break;
            case CardType.Skill:
                WingsTemporaryStrength++;
                await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, 1, Owner.Creature, cardPlay.Card);
                break;
            case CardType.Power:
                await PlayerCmd.GainEnergy(WingsEnergyGain, Owner);
                break;
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Mode == PunishingBirdPageMode.FlutteringWings)
        {
            await ClearWingsTemporaryBonuses();
        }

        if (side != CombatSide.Enemy || Mode != PunishingBirdPageMode.PunitiveBeak)
        {
            return;
        }

        if (!HpLostDuringCurrentRound && Owner.Creature.IsAlive)
        {
            Flash();
            BeakStrengthGranted += BeakStrength;
            BeakDexterityGranted += BeakDexterity;
            await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, BeakStrength, Owner.Creature, null);
            await PowerCmdCompat.Apply<DexterityPower>(Owner.Creature, BeakDexterity, Owner.Creature, null);
        }

        HpLostDuringCurrentRound = false;
    }

    private async Task ResetBeakBonuses()
    {
        if (BeakStrengthGranted > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, -BeakStrengthGranted, Owner.Creature, null);
            BeakStrengthGranted = 0;
        }

        if (BeakDexterityGranted > 0)
        {
            await PowerCmdCompat.Apply<DexterityPower>(Owner.Creature, -BeakDexterityGranted, Owner.Creature, null);
            BeakDexterityGranted = 0;
        }
    }

    private async Task ClearWingsTemporaryBonuses()
    {
        if (WingsTemporaryStrength > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, -WingsTemporaryStrength, Owner.Creature, null);
            WingsTemporaryStrength = 0;
        }

        if (WingsTemporaryDexterity > 0)
        {
            await PowerCmdCompat.Apply<DexterityPower>(Owner.Creature, -WingsTemporaryDexterity, Owner.Creature, null);
            WingsTemporaryDexterity = 0;
        }
    }

    private IReadOnlyList<CardModel> CreateChoiceCards() =>
    [
        Owner.RunState.CreateCard<PunishingBirdPunishmentChoiceCard>(Owner),
        Owner.RunState.CreateCard<PunishingBirdPunitiveBeakChoiceCard>(Owner),
        Owner.RunState.CreateCard<PunishingBirdFlutteringWingsChoiceCard>(Owner)
    ];

    private static bool IsKnownMode(PunishingBirdPageMode mode) =>
        mode is PunishingBirdPageMode.None
            or PunishingBirdPageMode.Punishment
            or PunishingBirdPageMode.PunitiveBeak
            or PunishingBirdPageMode.FlutteringWings;

    private static bool IsConcreteMode(PunishingBirdPageMode mode) =>
        mode is PunishingBirdPageMode.Punishment
            or PunishingBirdPageMode.PunitiveBeak
            or PunishingBirdPageMode.FlutteringWings;

    private void EnsureValidMode(string context)
    {
        if (!IsConcreteMode(Mode))
        {
            FallbackToDefaultMode(context);
        }
    }

    private void FallbackToDefaultMode(string context)
    {
        PunishingBirdPageMode oldMode = Mode;
        Mode = PunishingBirdPageMode.Punishment;
        HpLossSinceLastOwnerTurn = 0;
        HpLostDuringCurrentRound = false;
        BeakStrengthGranted = 0;
        BeakDexterityGranted = 0;
        WingsTemporaryStrength = 0;
        WingsTemporaryDexterity = 0;
        Log.Warn("[LibraryOfRuina.PageRelic] PunishingBirdPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Punishment.");
        UpdateModeUi();
    }

    private void UpdateModeUi()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        InvokeDisplayAmountChanged();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        BeakStrengthGranted = 0;
        BeakDexterityGranted = 0;
        return Task.CompletedTask;
    }
}

public enum PunishingBirdPageMode
{
    None = 0,
    Punishment = 1,
    PunitiveBeak = 2,
    FlutteringWings = 3
}
