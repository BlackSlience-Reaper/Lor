using System.Linq;
using MegaCrit.Sts2.Core.Commands.Builders;

namespace LibraryOfRuina.compat;

internal static class AttackCommandCompat
{
    public static IReadOnlyList<DamageResult> Results(AttackCommand command)
        => command.Results.SelectMany(static hitResults => hitResults).ToList();

    public static IReadOnlyList<DamageResult> Results(LibraryAttackCommand command)
        => command.DamageResults.SelectMany(static hitResults => hitResults).ToList();
}
