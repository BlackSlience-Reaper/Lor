using System;
using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.SmilingBodies;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SmilingBodies;

public abstract class SmilingBodiesPageChoiceCardBase : CardModel
{
    public const string CorpseLaughsChoiceId = "SMILING_BODIES_CORPSE_LAUGHS_CHOICE_CARD";
    public const string CorpseAbsorptionChoiceId = "SMILING_BODIES_CORPSE_ABSORPTION_CHOICE_CARD";
    public const string CorpseMountainChoiceId = "SMILING_BODIES_CORPSE_MOUNTAIN_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromPower<LibraryVulnerablePower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>(),
        HoverTipFactory.FromPower<LibraryQuicknessPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChaosDamage", SmilingBodiesPageRelic.CorpseLaughsChaosDamage),
        new DynamicVar("Vulnerable", SmilingBodiesPageRelic.CorpseLaughsVulnerable),
        new DynamicVar("HealPercent", SmilingBodiesPageRelic.AbsorptionHealPercent),
        new DynamicVar("HealUses", SmilingBodiesPageRelic.AbsorptionMaxHealsPerCombat),
        new DynamicVar("AllyBuffs", SmilingBodiesPageRelic.CorpseMountainBuffStacks),
        new DynamicVar("EnergyBonus", SmilingBodiesPageRelic.CorpseMountainEnergyBonusPerAlly),
        new DynamicVar("Cooldown", SmilingBodiesPageRelic.CorpseMountainCooldown),
        new DynamicVar("MaxHpLossPercent", SmilingBodiesPageRelic.CorpseMountainMaxHpLossPercent),
        new DynamicVar("LowHpThresholdPercent", SmilingBodiesPageRelic.CorpseLaughsLowHpThresholdPercent),
        new DynamicVar("LowHpThreshold", 0),
        new DynamicVar("HealAmount", 0),
        new DynamicVar("HealThreshold", 0),
        new DynamicVar("MaxHpLoss", 0)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected SmilingBodiesPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public override void AfterCreated()
    {
        base.AfterCreated();

        int ownerMaxHp = Owner.Creature.MaxHp;
        DynamicVars["LowHpThreshold"].BaseValue = ownerMaxHp > 0
            ? (int)(ownerMaxHp * SmilingBodiesPageRelic.CorpseLaughsLowHpThresholdPercent / 100m)
            : 0;
        DynamicVars["HealAmount"].BaseValue = ownerMaxHp > 0
            ? Math.Max(1, (int)Math.Ceiling(ownerMaxHp * SmilingBodiesPageRelic.AbsorptionHealPercent / 100m))
            : 0;
        DynamicVars["HealThreshold"].BaseValue = ownerMaxHp > 0
            ? Math.Max(1, (int)Math.Ceiling(ownerMaxHp * 10 / 100m))
            : 0;
        DynamicVars["MaxHpLoss"].BaseValue = ownerMaxHp > 0
            ? Math.Max(1, (int)Math.Ceiling(ownerMaxHp * SmilingBodiesPageRelic.CorpseMountainMaxHpLossPercent / 100m))
            : 0;
    }

    public static bool IsSmilingBodiesPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is CorpseLaughsChoiceId or CorpseAbsorptionChoiceId or CorpseMountainChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class SmilingBodiesCorpseLaughsChoiceCard : SmilingBodiesPageChoiceCardBase
{
    protected override string PortraitFileName => "smiling_bodies_corpse_laughs_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class SmilingBodiesCorpseAbsorptionChoiceCard : SmilingBodiesPageChoiceCardBase
{
    protected override string PortraitFileName => "smiling_bodies_corpse_absorption_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class SmilingBodiesCorpseMountainChoiceCard : SmilingBodiesPageChoiceCardBase
{
    protected override string PortraitFileName => "smiling_bodies_corpse_mountain_choice_card.png";
}
