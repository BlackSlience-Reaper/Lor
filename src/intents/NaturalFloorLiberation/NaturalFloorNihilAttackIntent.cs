using System;
using System.Linq;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

internal sealed class NaturalFloorNihilAttackIntent : AttackIntent,
    ITargetedIntentIndicator, IIntentTargetLineProvider, IUsesVanillaPlayerTargetIntentVisual
{
    private readonly NaturalFloorNihilMove _move;
    private readonly Func<Creature, IReadOnlyList<Creature>> _resolveTargets;
    private readonly string _descriptionKey;

    internal NaturalFloorNihilAttackIntent(NaturalFloorNihilMove move,
        Func<Creature, IReadOnlyList<Creature>> resolveTargets, string descriptionKey)
    {
        _move = move;
        _resolveTargets = resolveTargets;
        _descriptionKey = descriptionKey;
        DamageCalc = () => move.Damage;
    }

    public override int Repeats => _move.Hits;

    public bool UsesVanillaPlayerTargetIntentVisual => true;

    private int Damage(Creature owner) =>
        TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, _resolveTargets(owner).FirstOrDefault(), _move.Damage);

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) => Damage(owner) * Repeats;

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        var label = new LocString("intents", Repeats > 1 ? "FORMAT_DAMAGE_MULTI" : "FORMAT_DAMAGE_SINGLE");
        label.Add("Damage", Damage(owner));
        label.Add("Repeat", Repeats);
        return label;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        Creature? target = _resolveTargets(owner).FirstOrDefault();
        string key = target?.IsPlayer == true
            ? NaturalFloorLiberation.NaturalFloorNihilIntentText.PlayerDescriptionKey(_descriptionKey)
            : _descriptionKey;
        var description = new LocString("intents", key);
        NaturalFloorLiberation.NaturalFloorNihilIntentText.AddVariables(description, key, owner);
        description.Add("Damage", Damage(owner));
        description.Add("Repeat", Repeats);
        if (target?.IsPlayer != true)
        {
            description.Add("TargetName", target?.Name ?? "");
        }
        return description;
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(Creature owner, IReadOnlyList<Creature>? fallbackTargets) =>
        _resolveTargets(owner)
            .Select(target => new IntentTargetLineTarget(target, "NihilAttack"))
            .ToArray();
}
