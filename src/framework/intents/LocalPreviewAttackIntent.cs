using System;
using System.Linq;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents;

public sealed class LocalPreviewAttackIntent : AttackIntent
{
    private readonly Func<Creature, Creature?, int> _previewBaseDamageCalc;
    private readonly Func<int> _repeatCalc;
    private readonly string _descriptionKey;

    public LocalPreviewAttackIntent(
        Func<Creature, Creature?, int> previewBaseDamageCalc,
        Func<int>? repeatCalc,
        string descriptionKey)
    {
        ArgumentNullException.ThrowIfNull(previewBaseDamageCalc);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionKey);

        DamageCalc = () => 0m;
        _previewBaseDamageCalc = previewBaseDamageCalc;
        _repeatCalc = repeatCalc ?? (() => 1);
        _descriptionKey = descriptionKey;
    }

    public override int Repeats => Math.Max(1, _repeatCalc());

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetPreviewSingleDamage(owner) * Repeats;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetPreviewSingleDamage(owner));
        if (Repeats > 1)
        {
            fmt.Add("Repeat", Repeats);
        }

        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = new("intents", _descriptionKey);
        desc.Add("Damage", GetPreviewSingleDamage(owner));
        desc.Add("Repeat", Repeats);
        return desc;
    }

    private int GetPreviewSingleDamage(Creature owner)
    {
        Creature? previewTarget = GetPreviewTarget(owner);
        int baseDamage = _previewBaseDamageCalc(owner, previewTarget);
        return TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, previewTarget, baseDamage);
    }

    private static Creature? GetPreviewTarget(Creature owner)
    {
        if (owner.CombatState == null)
        {
            return null;
        }

        Player? localPlayer = LocalContextCompat.GetMe(owner.CombatState);
        if (localPlayer?.Creature is { IsAlive: true } localCreature)
        {
            return localCreature;
        }

        return owner.CombatState.PlayerCreatures.FirstOrDefault(static creature => creature.IsAlive)
            ?? owner.CombatState.GetOpponentsOf(owner).FirstOrDefault(static creature => creature.IsAlive);
    }
}
