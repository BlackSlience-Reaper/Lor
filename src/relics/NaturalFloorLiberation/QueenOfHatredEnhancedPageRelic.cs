using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.QueenOfHatred;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using LibraryOfRuina.powers.QueenOfHatred;
using LibraryOfRuina.relics.QueenOfHatred;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.NaturalFloorLiberation;

public sealed class QueenOfHatredEnhancedPageRelic : EnhancedMagicalGirlPageRelic<QueenOfHatredPageMode>
{
    // 博爱+：每次击破敌人格挡恢复的生命。
    public const int PhilanthropyHeal = 10;

    // 正义+：战斗开始时标记的敌人数。
    public const int JusticeTargets = 1;

    // 正义+：每个坏蛋标记的层数。
    public const int JusticeMarkAmount = 1;

    // 正义+：坏蛋受到的伤害和混乱伤害增幅百分比。
    public const int JusticeDamagePercent = 75;

    // 憎恶+：每次失去生命获得的强壮层数。
    public const int HatredStrong = 3;

    // 憎恶+：强壮持续的回合数。
    public const int HatredTurns = 2;

    protected override string IconBaseName => Mode switch
    {
        QueenOfHatredPageMode.Philanthropy => "queen_of_hatred_page_philanthropy_relic",
        QueenOfHatredPageMode.Justice => "queen_of_hatred_page_justice_relic",
        _ => "queen_of_hatred_page_hatred_relic"
    };

    [SavedProperty]
    public QueenOfHatredPageMode Mode { get; private set; }

    protected override QueenOfHatredPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", 0),
        new HealVar(PhilanthropyHeal),
        new DynamicVar("Targets", JusticeTargets),
        new DynamicVar("DamageIncrease", JusticeDamagePercent),
        new DynamicVar("Strong", HatredStrong),
        new DynamicVar("StrongTurns", HatredTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<NihilBadGuyPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        CreateUpgradedChoice<QueenOfHatredPhilanthropyChoiceCard>(),
        CreateUpgradedChoice<QueenOfHatredJusticeChoiceCard>(),
        CreateUpgradedChoice<QueenOfHatredHatredChoiceCard>()
    ];

    public override async Task BeforeCombatStart()
    {
        EnsureMode();
        if (Mode != QueenOfHatredPageMode.Justice)
        {
            return;
        }

        var enemies = AllyTurnRegistry.FilterPlayerEnemyTargets(Owner.Creature.CombatState?.Enemies)
            .Where(enemy => enemy.IsAlive && !enemy.HasPower<NihilBadGuyPower>())
            .ToList();
        var candidates = enemies
            .Where(enemy => !enemy.HasPower<LibraryOfRuinaQueenBadGuyPower>())
            .ToList();
        if (candidates.Count == 0)
        {
            candidates = enemies;
        }

        for (int i = 0; i < JusticeTargets && candidates.Count > 0; i++)
        {
            int index = Owner.RunState.Rng.Niche.NextInt(candidates.Count);
            Creature target = candidates[index];
            candidates.RemoveAt(index);
            // 其他玩家已施加普通坏蛋时，提升为强化标记，保持总增幅为强化值。
            await PowerCmdCompat.RemoveIfPresent<LibraryOfRuinaQueenBadGuyPower>(target);
            await PowerCmdCompat.Apply<NihilBadGuyPower>(target, JusticeMarkAmount, Owner.Creature, null);
        }
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (Mode == QueenOfHatredPageMode.Philanthropy
            && dealer != null
            && (dealer == Owner.Creature || dealer == Owner.Osty)
            && target.Side != Owner.Creature.Side && !AllyTurnRegistry.IsFriendlyAlly(target)
            && result.WasBlockBroken && Owner.Creature.IsAlive)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, PhilanthropyHeal);
        }
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (Mode == QueenOfHatredPageMode.Hatred && creature == Owner.Creature
            && delta < 0m && creature.IsAlive && CombatManager.Instance.IsInProgress)
        {
            Flash();
            await LibraryPowerCmd.Apply<LibraryStrongPower>(creature, HatredStrong,
                HatredTurns - 1, creature, null);
        }
    }
}
