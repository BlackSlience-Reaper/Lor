using System.Linq;

namespace LibraryOfRuina.encounters;

internal static class LiberationCombatEndGuard
{
    public static bool ShouldKeepCombatOpen(
        CombatStateLike combatState,
        int currentPhase,
        bool transitionPending,
        bool encounterComplete)
    {
        if (encounterComplete)
        {
            return false;
        }

        return transitionPending
            || combatState.Enemies.Any(enemy =>
                !enemy.IsAlive
                && enemy.Monster is ILiberationPrimaryPhaseBoss boss
                && boss.LiberationPhase == currentPhase);
    }
}
