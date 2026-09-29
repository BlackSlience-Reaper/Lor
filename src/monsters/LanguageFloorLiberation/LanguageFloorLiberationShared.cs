using System.Linq;
using LibraryOfRuina.combat;
using LibraryOfRuina.framework.combat;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

internal enum LanguageFloorMoveKind
{
    StableBreath,
    HuntTarget,
    HuntBeast,
    ExplosiveShot,
    DecisiveStrike,
    IndiscriminateShot,
    BrutalFangs,
    HorrifyingClaws,
    BloodstainedHunt,
    Howl,
    CobaltDoNotProvoke,
    CobaltCough,
    CobaltSharpClaws,
    CobaltWolfComes,
    BigWolfHorrifyingClaws,
    BigWolfBrutalFangs,
    BigWolfBloodstainedHunt,
    BigWolfShadowAssault,
    BigWolfUncontrollableInstinct,
    BigWolfRoar
}

internal static class LanguageFloorLiberationCombatHelper
{
    public static IReadOnlyList<Creature> GetLivingPlayers(Creature owner)
    {
        return CombatTargets.DeterministicLiving(
            owner.CombatState?.PlayerCreatures,
            owner);
    }

    public static IReadOnlyList<Creature> GetLivingPlayersAndCorpses(Creature owner)
    {
        if (owner.CombatState == null)
        {
            return [];
        }

        return CombatTargets.DeterministicLiving(
            owner.CombatState.PlayerCreatures.Concat(
                owner.CombatState.Enemies.Where(static creature =>
                creature.IsAlive
                && creature.Monster is LanguageFloorMeltingCorpse)),
            owner);
    }

    public static IReadOnlyList<Creature> GetLivingPlayersAndPartner(Creature owner)
    {
        if (owner.CombatState == null)
        {
            return [];
        }

        return CombatTargets.DeterministicLiving(
            owner.CombatState.Creatures.Where(creature =>
                creature.IsPlayer
                || creature.Monster is LanguageFloorScarletScar or LanguageFloorLostEverythingWolf),
            owner);
    }

    public static Creature? GetPartnerThenPlayerTarget(Creature owner)
    {
        return owner.CombatState?.Creatures
            .Where(creature => creature.IsAlive && creature != owner)
            .OrderBy(creature => creature.IsPlayer ? 1 : 0)
            .FirstOrDefault(creature =>
                creature.Monster is LanguageFloorScarletScar or LanguageFloorLostEverythingWolf)
            ?? owner.CombatState?.PlayerCreatures.FirstOrDefault(static player => player.IsAlive);
    }

    public static string GetPartnerThenPlayerTargetName(Creature owner) =>
        GetPartnerThenPlayerTarget(owner)?.Name ?? "未知目标";
}
