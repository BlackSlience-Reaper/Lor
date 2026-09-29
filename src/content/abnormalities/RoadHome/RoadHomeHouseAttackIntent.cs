using System;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.content.abnormalities.RoadHome;

public sealed class RoadHomeHouseAttackIntent :
    AttackIntent,
    IDetailedIntentVisuals,
    IIntentTargetLineProvider
{
    private readonly Func<int> _repeatCalc;
    private readonly string _descriptionKey;

    public RoadHomeHouseAttackIntent(
        Func<int> damageCalc,
        Func<int>? repeatCalc,
        string descriptionKey)
    {
        DamageCalc = () => damageCalc();
        _repeatCalc = repeatCalc ?? (() => 1);
        _descriptionKey = descriptionKey;
    }

    public override int Repeats => Math.Max(1, _repeatCalc());

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) =>
        GetSingleDamage(owner) * Repeats;

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetSingleDamage(owner));
        if (Repeats > 1)
        {
            fmt.Add("Repeat", Repeats);
        }

        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = new("intents", _descriptionKey);
        desc.Add("Damage", GetSingleDamage(owner));
        desc.Add("Repeat", Repeats);
        desc.Add("TargetName", GetHouseName(owner));
        return desc;
    }

    public DetailedIntentVisualState GetDetailedIntentVisuals(IEnumerable<Creature> targets, Creature owner) =>
        new([], RoadHomeEncounterHelper.FindHouse(owner.CombatState));

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets)
    {
        Creature? house = RoadHomeEncounterHelper.FindHouse(owner.CombatState);
        return house == null
            ? Array.Empty<IntentTargetLineTarget>()
            : [new IntentTargetLineTarget(house, "RoadHomeHouse")];
    }

    private int GetSingleDamage(Creature owner)
    {
        Creature? house = RoadHomeEncounterHelper.FindHouse(owner.CombatState);
        return TargetedAttackIntentPreviewHelper.GetModifiedDamage(
            owner,
            house,
            DamageCalc?.Invoke() ?? 0m);
    }

    private static string GetHouseName(Creature owner) =>
        RoadHomeEncounterHelper.FindHouse(owner.CombatState)?.Name ?? "家";
}
