using System;
using System.Linq;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents.BigBadWolf;

public sealed class WolfHowlIntent :
    AttackIntent,
    IBadgedIntent,
    IGroupAttackIntent,
    ITargetedIntentIndicator,
    IIntentTargetLineProvider
{
    private readonly IntentBadge _frailBadge;
    private readonly IntentBadge _groupAttackBadge;
    private readonly Func<Creature, IReadOnlyList<Creature>> _targetResolver;

    public WolfHowlIntent(
        Func<int> damageCalc,
        int frailAmount,
        Func<Creature, IReadOnlyList<Creature>> targetResolver)
    {
        ArgumentNullException.ThrowIfNull(damageCalc);
        ArgumentNullException.ThrowIfNull(targetResolver);

        DamageCalc = () => damageCalc();
        _targetResolver = targetResolver;
        _frailBadge = IntentBadge.Frail(frailAmount);
        _groupAttackBadge = IndiscriminateAttackIntent.CreateGroupAttackBadge();
        Badges = BadgedIntentBadgeList.Create(_frailBadge, _groupAttackBadge);
    }

    public IntentBadge Badge => _frailBadge;

    public IReadOnlyList<IntentBadge> Badges { get; }

    public bool IsGroupAttack => true;

    public override int Repeats => 1;

    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(Badges.SelectMany(badge => badge.AssetPaths));

    protected override string IntentPrefix => BadgedIntentTitle.GetPrefix(Badges, BadgedIntentMainIntent.Attack);

    protected override LocString IntentLabelFormat => new("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetSingleHowlDamage(owner);
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return BadgedIntentAnimation.GetAttackAnimation(GetTotalDamage(targets, owner));
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetSingleHowlDamage(owner));
        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create("WOLF_HOWL.description", owner, "ATTACK");
        desc.Add("Damage", GetSingleHowlDamage(owner));
        desc.Add("Repeat", Repeats);
        BadgedIntentDescription.AddBadgeVariables(desc, Badges);
        return desc;
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets)
    {
        return _targetResolver(owner)
            .Where(target => target is { IsAlive: true } && target != owner)
            .Distinct()
            .Select(static target => new IntentTargetLineTarget(target, "WolfHowl"))
            .ToArray();
    }

    private int GetSingleHowlDamage(Creature owner)
    {
        Creature? target = GetPreviewTarget(owner);
        return TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, DamageCalc?.Invoke() ?? 0m);
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
            ?? owner.CombatState.Creatures.FirstOrDefault(creature => creature.IsAlive && creature != owner);
    }
}
