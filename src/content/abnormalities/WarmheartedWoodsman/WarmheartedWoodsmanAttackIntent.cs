using System;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;

/// <summary>
/// 樵夫的纯攻击意图：攻击玩家时沿用原版伤害预览、不显示玩家姓名；
/// 樵夫按 <see cref="ITargetedMonsterAttackProvider"/> 改打树木时，伤害按树木结算预览并写出树木名称。
/// </summary>
public sealed class WarmheartedWoodsmanAttackIntent :
    AttackIntent,
    ITargetedIntentIndicator,
    IUsesVanillaPlayerTargetIntentVisual
{
    private readonly Func<int> _repeatCalc;
    private readonly string _playerTargetDescriptionKey;
    private readonly string? _treeTargetDescriptionKey;

    public WarmheartedWoodsmanAttackIntent(
        Func<int> damageCalc,
        Func<int> repeatCalc,
        string playerTargetDescriptionKey,
        string? treeTargetDescriptionKey = null)
    {
        DamageCalc = () => damageCalc();
        _repeatCalc = repeatCalc;
        _playerTargetDescriptionKey = playerTargetDescriptionKey;
        _treeTargetDescriptionKey = treeTargetDescriptionKey;
    }

    public override int Repeats => Math.Max(1, _repeatCalc());

    public bool UsesVanillaPlayerTargetIntentVisual => true;

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetDisplayedSingleDamage(targets, owner) * Repeats;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetDisplayedSingleDamage(targets, owner));
        if (Repeats > 1)
        {
            fmt.Add("Repeat", Repeats);
        }

        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        Creature? treeTarget = GetTreeTarget(owner);
        bool describesTree = treeTarget != null && _treeTargetDescriptionKey != null;
        LocString desc = new(
            "intents",
            describesTree ? _treeTargetDescriptionKey! : _playerTargetDescriptionKey);
        desc.Add("Damage", GetDisplayedSingleDamage(targets, owner));
        desc.Add("Repeat", Repeats);
        desc.Add("IsMultiplayer", owner.CombatState != null && owner.CombatState.RunState.Players.Count > 1);
        if (describesTree)
        {
            desc.Add("TargetName", treeTarget!.Name);
        }

        return desc;
    }

    private int GetDisplayedSingleDamage(IEnumerable<Creature> targets, Creature owner)
    {
        Creature? treeTarget = GetTreeTarget(owner);
        return treeTarget == null
            ? GetSingleDamage(targets, owner)
            : TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, treeTarget, DamageCalc?.Invoke() ?? 0m);
    }

    private static Creature? GetTreeTarget(Creature owner)
    {
        if (!TargetedMonsterAttackHelper.TryGetProvider(owner, out ITargetedMonsterAttackProvider? provider)
            || provider == null
            || !provider.UsesTargetedAttackContract(owner))
        {
            return null;
        }

        return TargetedMonsterAttackHelper.GetPrimaryTarget(owner);
    }
}
