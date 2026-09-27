namespace LibraryOfRuina.intents;

internal static class FoxIntentTargetHelper
{
    public static Creature? GetPrimaryEnemyTarget(Creature owner)
    {
        return TargetedMonsterAttackHelper.GetPrimaryTarget(owner);
    }

    public static IReadOnlyList<Creature> GetPrimaryEnemyTargetList(Creature owner)
    {
        return TargetedMonsterAttackHelper.GetTargetList(owner);
    }

    public static string GetPrimaryEnemyTargetName(Creature owner)
    {
        return TargetedMonsterAttackHelper.GetPrimaryTargetName(owner);
    }
}
