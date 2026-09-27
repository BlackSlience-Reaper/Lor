using System;
using System.Linq;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents.WarmheartedWoodsman;

public sealed class WarmheartedWoodsmanLoggingFinalIntent : AttackIntent
{
    private readonly Func<int> _setupDamageCalc;
    private readonly Func<int> _finalDamageCalc;
    private readonly string _descriptionKey;

    public WarmheartedWoodsmanLoggingFinalIntent(
        Func<int> setupDamageCalc,
        Func<int> finalDamageCalc,
        string descriptionKey)
    {
        _setupDamageCalc = setupDamageCalc;
        _finalDamageCalc = finalDamageCalc;
        _descriptionKey = descriptionKey;
        DamageCalc = () => finalDamageCalc();
    }

    protected override LocString IntentLabelFormat => new("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetPreview(owner, targets as IReadOnlyList<Creature>).FinalDamage;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetPreview(owner, targets as IReadOnlyList<Creature>).FinalDamage);
        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        AttackPreview preview = GetPreview(owner, targets as IReadOnlyList<Creature>);
        LocString desc = new("intents", _descriptionKey);
        desc.Add("Damage", preview.FinalDamage);
        desc.Add("Repeat", 1);
        desc.Add("FullyBlocked", preview.FullyBlockedSetupHits);
        desc.Add("Reduction", preview.Reduction);
        return desc;
    }

    private AttackPreview GetPreview(Creature owner, IReadOnlyList<Creature>? fallbackTargets)
    {
        IReadOnlyList<Creature> targets = TargetedMonsterAttackHelper.GetTargetList(owner, fallbackTargets);
        int fullyBlocked = targets.Sum(target => CountFullyBlockedSetupHits(
            TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, _setupDamageCalc()),
            target.Block));
        int reduction = fullyBlocked * monsters.WarmheartedWoodsman.WarmheartedWoodsman.LoggingFinalDamageLossPerFullBlock;
        int reducedBaseDamage = Math.Max(0, _finalDamageCalc() - reduction);
        Creature? previewTarget = LocalContext.GetMe(owner.CombatState)?.Creature;
        if (previewTarget == null || !targets.Contains(previewTarget))
        {
            previewTarget = targets.FirstOrDefault();
        }

        int finalDamage = reducedBaseDamage <= 0
            ? 0
            : TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, previewTarget, reducedBaseDamage);

        return new AttackPreview(
            finalDamage,
            fullyBlocked,
            reduction);
    }

    private static int CountFullyBlockedSetupHits(int setupDamage, int startingBlock)
    {
        if (setupDamage <= 0 || startingBlock <= 0)
        {
            return 0;
        }

        int fullyBlocked = 0;
        int remainingBlock = startingBlock;
        for (int i = 0; i < monsters.WarmheartedWoodsman.WarmheartedWoodsman.LoggingSetupHits; i++)
        {
            if (remainingBlock >= setupDamage)
            {
                fullyBlocked++;
                remainingBlock -= setupDamage;
                continue;
            }

            remainingBlock = 0;
        }

        return fullyBlocked;
    }

    private readonly record struct AttackPreview(
        int FinalDamage,
        int FullyBlockedSetupHits,
        int Reduction);
}
