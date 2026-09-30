using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.specialguests.Xiao;

public abstract class XiaoEgoCardBase(
    int energyCost,
    CardType cardType,
    TargetType targetType,
    string portraitAssetPath)
    : CardModel(energyCost,
        cardType,
        CardRarity.Rare,
        targetType,
        shouldShowInCardLibrary: true)
{
    public override int MaxUpgradeLevel => 1;

    public override CardPoolModel VisualCardPool =>
        ModelDb.CardPool<LibraryOfRuinaCharacterEgoCardPool>();

    public override string PortraitPath =>
        ImageHelper.GetImagePath(portraitAssetPath);

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];
}

public sealed class XiaoPulaoBellEgoCard() : XiaoEgoCardBase(1,
    CardType.Attack,
    TargetType.AllEnemies,
    PortraitAssetPath)
{
    public const string PortraitAssetPath =
        "packed/card_portraits/character_ego/xiao_pulao_bell_ego_card.png";

    private const string StrikeDieName = "StrikeDie";
    private const string DieMinimumDisplayName = "Die1Min";
    private const string DieMaximumDisplayName = "Die1Max";
    private const string WeakName = "Weak";
    private const string FlawName = "Flaw";
    private const int DiceDescriptionIconSize = 25;

    private const int BaseDieMinimum = 12;
    private const int BaseDieMaximum = 15;
    private const int UpgradedDieMinimum = 14;
    private const int UpgradedDieMaximum = 18;
    private const int BaseDebuffAmount = 1;
    private const int UpgradedDebuffAmount = 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new LibraryDice(
            BaseDieMinimum,
            BaseDieMaximum - BaseDieMinimum,
            LibraryDiceType.Blunt,
            this,
            StrikeDieName),
        new DynamicVar(DieMinimumDisplayName, BaseDieMinimum),
        new DynamicVar(DieMaximumDisplayName, BaseDieMaximum),
        new PowerVar<LibraryWeakPower>(WeakName, BaseDebuffAmount),
        new PowerVar<LibraryDisarmPower>(FlawName, BaseDebuffAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
        HoverTipFactory.FromPower<LibraryWeakPower>(),
        HoverTipFactory.FromPower<LibraryDisarmPower>()
    ];

    private LibraryDice StrikeDie =>
        (LibraryDice)DynamicVars[StrikeDieName];

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "Die1Icon",
            $"[img={DiceDescriptionIconSize}x{DiceDescriptionIconSize}]"
            + $"{StrikeDie.DescriptionIconPath}[/img]");
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException(
                "Xiao's Pulao Bell requires an active combat state.");

        await LibraryDamageCmd.Attack(StrikeDie)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .Unblockable()
            .WithHitFx("vfx/vfx_attack_blunt")
            .SpawningHitVfxOnEachCreature()
            .Execute(choiceContext, cardPlay);

        Creature[] targets = combatState.LivingHittableEnemies()
            .OrderBy(static target => target.CombatId)
            .ToArray();
        foreach (Creature target in targets)
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                choiceContext,
                target,
                DynamicVars[WeakName].BaseValue,
                turns: -1,
                IsPermanent: true,
                Owner.Creature,
                this);
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(
                choiceContext,
                target,
                DynamicVars[FlawName].BaseValue,
                turns: -1,
                IsPermanent: true,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        StrikeDie.UpgradeValueBy(UpgradedDieMinimum - BaseDieMinimum);
        StrikeDie.FloatValue = UpgradedDieMaximum - UpgradedDieMinimum;
        DynamicVars[DieMinimumDisplayName].UpgradeValueBy(
            UpgradedDieMinimum - BaseDieMinimum);
        DynamicVars[DieMaximumDisplayName].UpgradeValueBy(
            UpgradedDieMaximum - BaseDieMaximum);
        DynamicVars[WeakName].UpgradeValueBy(
            UpgradedDebuffAmount - BaseDebuffAmount);
        DynamicVars[FlawName].UpgradeValueBy(
            UpgradedDebuffAmount - BaseDebuffAmount);
    }
}

public sealed class XiaoYaziVengeanceEgoCard() : XiaoEgoCardBase(0,
    CardType.Skill,
    TargetType.Self,
    PortraitAssetPath)
{
    public const string PortraitAssetPath =
        "packed/card_portraits/character_ego/xiao_yazi_vengeance_ego_card.png";

    private const string BurnName = "Burn";
    private const int BaseBurnPerCard = 4;
    private const int UpgradedBurnPerCard = 6;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<LibraryBurnPower>(BurnName, BaseBurnPerCard)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<XiaoYaziVengeancePower>(),
        HoverTipFactory.FromPower<LibraryBurnPower>()
    ];

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmdCompat.Apply<XiaoYaziVengeancePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[BurnName].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[BurnName].UpgradeValueBy(
            UpgradedBurnPerCard - BaseBurnPerCard);
    }
}

public sealed class XiaoTaotieFeastEgoCard : XiaoEgoCardBase
{
    public const string PortraitAssetPath =
        "packed/card_portraits/character_ego/xiao_taotie_feast_ego_card.png";

    public XiaoTaotieFeastEgoCard()
        : base(
            2,
            CardType.Power,
            TargetType.Self,
            PortraitAssetPath)
    {
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<XiaoTaotieFeastPower>(),
        HoverTipFactory.FromPower<LibraryBurnPower>()
    ];

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmdCompat.Ensure<XiaoTaotieFeastPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }
}
