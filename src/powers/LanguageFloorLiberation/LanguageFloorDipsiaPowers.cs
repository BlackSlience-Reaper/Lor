using System;
using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.LanguageFloorLiberation;

public abstract class LanguageFloorDipsiaPowerModel
    : LibraryOfRuinaPowerModel
{
    protected abstract string ReusedIconFileName { get; }

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/" + ReusedIconFileName);

    public override string ResolvedBigIconPath => PackedIconPath;
}

public sealed class LanguageFloorDipsiaHydrophobiaPassivePower
    : LanguageFloorDipsiaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_DIPSIA_HYDROPHOBIA_PASSIVE_POWER";

    protected override string ReusedIconFileName =>
        "nosferatu_hydrophobia_passive_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "DipsiaThreshold",
            LanguageFloorDipsia.HydrophobiaBloodThreshold),
        new DynamicVar(
            "DipsiaStrong",
            LanguageFloorDipsia.HydrophobiaStrong),
        new DynamicVar(
            "BatThreshold",
            LanguageFloorBloodBat.HydrophobiaBloodThreshold),
        new DynamicVar(
            "BatStrong",
            LanguageFloorBloodBat.HydrophobiaStrong)
    ];
}

public sealed class LanguageFloorDipsiaTransformPower
    : LanguageFloorDipsiaPowerModel, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_DIPSIA_TRANSFORM_POWER";

    protected override string ReusedIconFileName =>
        "nosferatu_transform_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "HpPercent",
            LanguageFloorDipsia.TransformHpPercent)
    ];

    public bool IsPendingTransform =>
        Owner.Monster is LanguageFloorDipsia
        {
            IsTransformed: false,
            TransformPending: true
        };

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || amount <= 0m
            || Owner.Monster is not LanguageFloorDipsia
                { IsTransformed: false } dipsia)
        {
            return amount;
        }

        if (dipsia.TransformPending)
        {
            return 0m;
        }

        decimal maxLossBeforeTransform =
            Owner.CurrentHp - dipsia.TransformHpThreshold;
        if (maxLossBeforeTransform <= 0m)
        {
            return amount;
        }

        // 伤害预览也会调用此钩子；变身标记只在实际生命变化后写入。
        return Math.Min(amount, maxLossBeforeTransform);
    }

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta)
    {
        if (creature != Owner
            || delta >= 0m
            || Owner.Monster is not LanguageFloorDipsia
                { IsTransformed: false } dipsia)
        {
            return;
        }

        if (Owner.CurrentHp <= dipsia.TransformHpThreshold)
        {
            await dipsia.QueueTransformAndClampHp();
        }
    }

    public override bool ShouldDie(Creature creature)
    {
        if (creature != Owner
            || Owner.Monster is not LanguageFloorDipsia
                { IsTransformed: false } dipsia)
        {
            return true;
        }

        if (dipsia.TransformPending
            || Owner.CurrentHp <= dipsia.TransformHpThreshold)
        {
            return false;
        }

        return !WouldBeKilledByDoom();
    }

    // Doom checks ShouldDie before Kill zeroes HP. The 50% transform lock is
    // active before transformation, so a lethal Doom must already count as
    // prevented and restore to the transform threshold.
    private bool WouldBeKilledByDoom() =>
        Owner.GetPower<DoomPower>() is { } doom
        && Owner.CurrentHp <= doom.Amount;

    public override Task AfterPreventingDeath(Creature creature)
    {
        return creature == Owner
            && Owner.Monster is LanguageFloorDipsia
                { IsTransformed: false } dipsia
                ? dipsia.QueueTransformAndClampHp()
                : Task.CompletedTask;
    }
}
