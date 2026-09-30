using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Language;

public abstract class LanguageFloorMimicryEvolutionPower
    : LibraryFakeDeathPowerModel
{
    protected override bool IsOwnerFakeDead =>
        Owner.Monster is LanguageFloorMimicry { IsFakeDead: true };

    protected override bool CanEnterFakeDeath(Creature creature) =>
        Owner.Monster is LanguageFloorMimicry boss
        && boss.CanEnterFakeDeath(creature);

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is LanguageFloorMimicry boss
            ? boss.EnterFakeDeathFromDeath()
            : Task.CompletedTask;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class LanguageFloorMimicryFormOneEvolutionPower
    : LanguageFloorMimicryEvolutionPower
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_MIMICRY_FORM_ONE_EVOLUTION_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("NextForm", 2)];
}

public sealed class LanguageFloorMimicryFormTwoEvolutionPower
    : LanguageFloorMimicryEvolutionPower
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_MIMICRY_FORM_TWO_EVOLUTION_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "Actions",
            LanguageFloorMimicry.FormTwoActionsBeforeEvolution),
        new DynamicVar(
            "MinimumHpPercent",
            LanguageFloorMimicry.FormThreeMinimumHpPercent)
    ];
}

public sealed class LanguageFloorMimicryFormTwoRegenerationPower
    : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_MIMICRY_FORM_TWO_REGENERATION_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("HealPercent", 50)];
}

public sealed class LanguageFloorMimicryHardenPower
    : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_MIMICRY_HARDEN_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Threshold", LanguageFloorMimicry.HardenThreshold)];

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return target == Owner
            && Owner.Monster is LanguageFloorMimicry
                { Form: LanguageFloorMimicryForm.Third }
            && amount <= LanguageFloorMimicry.HardenThreshold
            && ValuePropCompat.IsPoweredAttack(props)
                ? 0m
                : 1m;
    }
}

public sealed class LanguageFloorMimicryFormThreeRegenerationPower
    : LibraryOfRuinaPowerModel
{
    private sealed class DamageThresholdVar()
        : DynamicVar(
            "DamageThreshold",
            LanguageFloorMimicry.FormThreeRegenerationDamageThreshold)
    {
        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner
                is LanguageFloorMimicryFormThreeRegenerationPower
                    { IsMutable: true } power
                && power.Owner.Monster is LanguageFloorMimicry mimicry
                    ? mimicry.ScaledFormThreeRegenerationDamageThreshold
                    : BaseValue;
        }

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString();
    }

    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_MIMICRY_FORM_THREE_REGENERATION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageThresholdVar(),
        new DynamicVar(
            "HealPercent",
            LanguageFloorMimicry.FormThreeRegenerationPercent)
    ];
}

public sealed class LanguageFloorMimicryMimicPower
    : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_MIMICRY_MIMIC_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Maximum", LanguageFloorMimicry.MaximumMimicStacks),
        new DynamicVar("Decay", LanguageFloorMimicry.MimicDecayPerTurn)
    ];
}
