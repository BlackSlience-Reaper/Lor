using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.infra.helpers;

internal static class AbnormalityAnimHelper
{
    public const float DefaultAttackSegmentDelaySeconds = 1.95f;
    public const float DefaultCastDelaySeconds = 1.95f;

    public static Task TriggerCast(Creature creature, float delaySeconds = DefaultCastDelaySeconds)
    {
        return CreatureCmd.TriggerAnim(creature, "Cast", delaySeconds);
    }

    public static Task ExecuteAttackSegment(
        MonsterModel monster,
        int damage,
        string animId = "Attack",
        float delaySeconds = DefaultAttackSegmentDelaySeconds,
        string hitFxPath = "vfx/vfx_attack_slash",
        Action<dynamic>? configure = null)
    {
        dynamic builder = DamageCmd.Attack(damage)
            .FromMonster(monster)
            .WithAttackerAnim(animId, delaySeconds)
            .WithHitFx(hitFxPath);

        configure?.Invoke(builder);
        return builder.Execute(null);
    }
}

