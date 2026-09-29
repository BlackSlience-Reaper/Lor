using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using LibraryOfRuina.framework.intents;

namespace LibraryOfRuina.intents.QueenOfHatred;

public sealed class QueenLightOfHatredIntent : AttackIntent, ITargetedIntentIndicator
{
    public QueenLightOfHatredIntent()
    {
        DamageCalc = () => 0;
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        AttackPreview preview = GetPreview(owner, targets as IReadOnlyList<Creature>);
        return preview.FirstDamage + preview.SecondDamage;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = new("intents", "FORMAT_DAMAGE_SINGLE");
        fmt.Add("Damage", GetTotalDamage(targets, owner));
        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        AttackPreview preview = GetPreview(owner, targets as IReadOnlyList<Creature>);
        LocString desc = new("intents", "QUEEN_LIGHT_OF_HATRED.description");
        desc.Add("FirstDamage", preview.FirstDamage);
        desc.Add("SecondDamage", preview.SecondDamage);
        desc.Add("HealAmount", preview.HealAmount);
        desc.Add("TargetName", preview.TargetName);
        return desc;
    }

    private static AttackPreview GetPreview(Creature owner, IReadOnlyList<Creature>? fallbackTargets)
    {
        if (owner.Monster is not monsters.QueenOfHatred.QueenOfHatred queen)
        {
            return AttackPreview.Empty;
        }

        Creature? target = TargetedMonsterAttackHelper.GetPrimaryTarget(owner, fallbackTargets);
        int firstDamage = TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, queen.SnakeLightOfHatredFirstDamage);
        int secondDamage = TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, queen.SnakeLightOfHatredSecondDamage);
        int remainingBlock = target?.Block ?? 0;

        if (remainingBlock > 0)
        {
            remainingBlock = remainingBlock - firstDamage;
            if (remainingBlock < 0)
            {
                remainingBlock = 0;
            }
        }

        int healAmount = secondDamage;
        if (remainingBlock > 0)
        {
            healAmount -= remainingBlock;
            if (healAmount < 0)
            {
                healAmount = 0;
            }
        }

        return new AttackPreview(
            firstDamage,
            secondDamage,
            healAmount,
            target?.Name ?? "Unknown Target");
    }

    private readonly record struct AttackPreview(
        int FirstDamage,
        int SecondDamage,
        int HealAmount,
        string TargetName)
    {
        public static AttackPreview Empty => new(0, 0, 0, "Unknown Target");
    }
}
