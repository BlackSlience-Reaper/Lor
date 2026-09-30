using System.Linq;
using System.Threading.Tasks;
using System;
using LibraryOfRuina.content.abnormalities.KingOfGreed;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class KingOfGreedEnhancedPageRelic : EnhancedMagicalGirlPageRelic<KingOfGreedPageMode>
{
    // 放纵+：每隔这些自身回合，击晕所有存活敌人。
    public const int IndulgenceTurns = 6;

    // 放纵+：击晕目标的回合数。
    public const int IndulgenceStunTurns = 1;

    // 幸福之路+：每次获得守护所需的未格挡伤害次数。
    public const int HappinessDamageEvents = 1;

    // 幸福之路+：每次未格挡伤害获得的守护层数。
    public const int HappinessProtection = 1;

    // 幸福之路+：每回合最多获得的守护层数。
    public const int HappinessLimit = 3;

    // 幸福之路+：守护持续的回合数。
    public const int HappinessTurns = 1;

    // 贪婪+：未被格挡的攻击伤害转为治疗的百分比。
    public const int LifestealPercent = 40;

    protected override string IconBaseName => "king_of_greed_page_relic";

    public override bool ShowCounter =>
        CombatManager.Instance.IsInProgress
        && Mode is KingOfGreedPageMode.Indulgence or KingOfGreedPageMode.HappinessPath;

    public override int DisplayAmount => Mode == KingOfGreedPageMode.Indulgence
        ? TurnsUntilStun : HappinessGainedThisTurn;

    [SavedProperty]
    public int TurnsUntilStun { get; private set; }

    [SavedProperty]
    public int StunTurnsRemaining { get; private set; }

    [SavedProperty]
    public int HappinessDamageEventsThisTurn { get; private set; }

    [SavedProperty]
    public int HappinessGainedThisTurn { get; private set; }

    [SavedProperty]
    public KingOfGreedPageMode Mode { get; private set; }

    protected override KingOfGreedPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", 0),
        new DynamicVar("BlockTurns", IndulgenceTurns),
        new DynamicVar("StunTurns", IndulgenceStunTurns),
        new DynamicVar("DamageEvents", HappinessDamageEvents),
        new DynamicVar("Protection", HappinessProtection),
        new DynamicVar("MaxProtection", HappinessLimit),
        new DynamicVar("Turns", HappinessTurns),
        new DynamicVar("LifestealPercent", LifestealPercent)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Stun),
        HoverTipFactory.FromPower<LibraryProtectionPower>()
    ];

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        CreateUpgradedChoice<KingOfGreedIndulgenceChoiceCard>(),
        CreateUpgradedChoice<KingOfGreedHappinessPathChoiceCard>(),
        CreateUpgradedChoice<KingOfGreedGreedChoiceCard>()
    ];

    public override Task BeforeCombatStart()
    {
        EnsureMode();
        TurnsUntilStun = IndulgenceTurns;
        HappinessGainedThisTurn = 0;
        HappinessDamageEventsThisTurn = 0;
        StunTurnsRemaining = 0;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != Owner.Creature.Side || !participants.Contains(Owner.Creature))
        {
            return;
        }

        HappinessGainedThisTurn = 0;
        HappinessDamageEventsThisTurn = 0;
        if (Mode == KingOfGreedPageMode.Indulgence)
        {
            TurnsUntilStun--;
            if (TurnsUntilStun <= 0)
            {
                TurnsUntilStun = IndulgenceTurns;
                StunTurnsRemaining = IndulgenceStunTurns;
            }

            if (StunTurnsRemaining > 0)
            {
                StunTurnsRemaining--;
                Creature[] enemies = AllyTurnRegistry.FilterPlayerEnemyTargets(combatState.Enemies)
                    .Where(enemy => enemy.IsAlive)
                    .ToArray();
                Flash(enemies);
                foreach (Creature enemy in enemies)
                {
                    await CreatureCmd.Stun(enemy);
                }
            }
        }

        UpdateModeUiState();
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer == null || (dealer != Owner.Creature && dealer != Owner.Osty))
        {
            return;
        }

        if (result.UnblockedDamage <= 0m || !Owner.Creature.IsAlive)
        {
            return;
        }

        if (Mode == KingOfGreedPageMode.HappinessPath && HappinessGainedThisTurn < HappinessLimit
            && Owner.Creature.CombatState?.CurrentSide == Owner.Creature.Side)
        {
            HappinessDamageEventsThisTurn++;
            if (HappinessDamageEventsThisTurn % HappinessDamageEvents != 0)
            {
                return;
            }

            int gain = Math.Min(HappinessProtection, HappinessLimit - HappinessGainedThisTurn);
            HappinessGainedThisTurn += gain;
            await LibraryPowerCmd.Apply<LibraryProtectionPower>(Owner.Creature, gain,
                HappinessTurns - 1, Owner.Creature, cardSource);
            UpdateModeUiState();
        }

        if (Mode == KingOfGreedPageMode.Greed && ValuePropCompat.IsPoweredAttack(props))
        {
            int heal = (int)Math.Floor(result.UnblockedDamage * LifestealPercent / 100m);
            if (heal > 0)
            {
                await CreatureCmd.Heal(Owner.Creature, heal);
            }
        }
    }
}
