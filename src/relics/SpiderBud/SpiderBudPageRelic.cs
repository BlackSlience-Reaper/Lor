using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.SpiderBud;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.SpiderBud;

public sealed class SpiderBudPageRelic : ModalPageRelic<SpiderBudPageMode>
{
    internal const int CocoonDeckSizeThreshold = 25;
    internal const int CocoonStrength = 2;
    internal const int CocoonDexterity = 2;
    internal const int FeedingHeal = 12;
    internal const int FeedingPoison = 3;
    internal const int VigilanceStrengthLoss = 1;
    internal const int VigilancePermanentVulnerable = 4;

    protected override string IconBaseName => "spider_bud_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<SpiderBudPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)SpiderBudPageMode.None),
        new DynamicVar("DeckSizeThreshold", CocoonDeckSizeThreshold),
        new DynamicVar("Strength", CocoonStrength),
        new DynamicVar("Dexterity", CocoonDexterity),
        new HealVar(FeedingHeal),
        new DynamicVar("Poison", FeedingPoison),
        new DynamicVar("StrengthLoss", VigilanceStrengthLoss),
        new DynamicVar("Vulnerable", VigilancePermanentVulnerable)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        SpiderBudPageMode.CocoonBind =>
        [
            HoverTipFactory.FromPower<StrengthPower>(),
            HoverTipFactory.FromPower<DexterityPower>()
        ],
        SpiderBudPageMode.Feeding =>
        [
            HoverTipFactory.FromPower<PoisonPower>()
        ],
        SpiderBudPageMode.Vigilance =>
        [
            HoverTipFactory.FromPower<LibraryVulnerablePower>(),
            HoverTipFactory.FromPower<LibraryBreakVulnerablePower>()
        ],
        _ => []
    };

    [SavedProperty]
    public SpiderBudPageMode Mode { get; private set; }

    protected override SpiderBudPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));

        if (Owner.Creature == null)
        {
            UpdateModeUiState();
            return;
        }

        if (Mode == SpiderBudPageMode.CocoonBind)
        {
            Flash();
            if (Owner.Deck.Cards.Count <= CocoonDeckSizeThreshold)
            {
                await PowerCmdCompat.Apply<StrengthPower>(
                    Owner.Creature, CocoonStrength, Owner.Creature, null, silent: true);
                await PowerCmdCompat.Apply<DexterityPower>(
                    Owner.Creature, CocoonDexterity, Owner.Creature, null, silent: true);
            }
            else
            {
                await PowerCmdCompat.Apply<DexterityPower>(
                    Owner.Creature, -1, Owner.Creature, null, silent: true);
            }
        }
        else if (Mode == SpiderBudPageMode.Vigilance)
        {
            await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, -VigilanceStrengthLoss, Owner.Creature, null, silent: true);

            IReadOnlyList<Creature> enemies = AllyTurnRegistry
                .FilterPlayerEnemyTargets(Owner.Creature.CombatState?.Enemies)
                .Where(e => e.IsAlive)
                .ToArray();

            if (enemies.Count > 0)
            {
                foreach (Creature enemy in enemies)
                {
                    await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                        enemy, VigilancePermanentVulnerable, turns: -1, Owner.Creature, null, silent: true);
                    await LibraryPowerCmd.Apply<LibraryBreakVulnerablePower>(
                        enemy, VigilancePermanentVulnerable, turns: -1, Owner.Creature, null, silent: true);
                }
            }
        }
        else if (Mode == SpiderBudPageMode.Feeding)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, FeedingHeal);
            await PowerCmdCompat.Apply<PoisonPower>(Owner.Creature, FeedingPoison, Owner.Creature, null, silent: true);
        }

        UpdateModeUiState();
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<SpiderBudCocoonBindChoiceCard>(Owner),
            Owner.RunState.CreateCard<SpiderBudFeedingChoiceCard>(Owner),
            Owner.RunState.CreateCard<SpiderBudVigilanceChoiceCard>(Owner)
        ];
    }

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }
}
