using System.Linq;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using QueenBeeMonster = LibraryOfRuina.monsters.QueenBee.QueenBee;

namespace LibraryOfRuina.intents.QueenBee;

public sealed class QueenBeeDeathEmbraceIntent : AttackIntent
    , IIntentTargetLineProvider
{
    public QueenBeeDeathEmbraceIntent(int damage)
    {
        DamageCalc = () => damage;
    }

    public override int Repeats => 1;

    protected override string IntentPrefix => "QUEEN_BEE_WORKER_DEATH_EMBRACE";

    public override string GetAnimation(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        // Custom IntentPrefix has no registered entry in IntentAnimData, so fall back
        // to the vanilla attack tier keys (matches AttackIntent.GetTexture tiering).
        int totalDamage = GetTotalDamage(targets, owner);
        return totalDamage < 5 ? IntentAnimData.attack1
            : totalDamage < 10 ? IntentAnimData.attack2
            : totalDamage < 20 ? IntentAnimData.attack3
            : totalDamage < 40 ? IntentAnimData.attack4
            : IntentAnimData.attack5;
    }

    protected override LocString IntentLabelFormat =>
        new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        return GetSingleDamage(owner);
    }

    public override LocString GetIntentLabel(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString format = IntentLabelFormat;
        format.Add("Damage", GetSingleDamage(owner));
        return format;
    }

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString desc = new LocString(
            "intents",
            "QUEEN_BEE_WORKER_DEATH_EMBRACE.description");
        desc.Add("Damage", GetSingleDamage(owner));
        desc.Add("Repeat", Repeats);
        return desc;
    }

    private int GetSingleDamage(Creature owner)
    {
        Creature? queen = FindQueen(owner);
        return TargetedAttackIntentPreviewHelper.GetModifiedDamage(
            owner,
            queen,
            DamageCalc?.Invoke() ?? 0m);
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets)
    {
        Creature? queen = FindQueen(owner);
        return queen == null
            ? []
            : [new IntentTargetLineTarget(queen, "QueenBee")];
    }

    private static Creature? FindQueen(Creature owner)
    {
        return owner.CombatState?.Enemies
            .FirstOrDefault(
                static enemy => enemy.IsAlive
                    && enemy.Monster is QueenBeeMonster);
    }
}
