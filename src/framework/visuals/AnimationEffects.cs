using LibraryOfRuina.core.settings;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.framework.visuals;

/// <summary>
/// “动画效果”设置的读取入口。开=原版分层生成的 Spine 骨骼动画（<see cref="RuntimeSpineBody"/>，加载失败的怪物退回逐帧换图），
/// 关=逐帧换图、瞬间切换（Spine 化之前的样子）。只影响本机外观，不参与联机比对。
/// Spine 身体在怪物出场时按当时的开关建立，局内切换对之后出场的怪物生效。
/// </summary>
internal static class AnimationEffects
{
    internal static bool SpineEnabled => LibraryOfRuinaSettings.AnimationEffectsEnabled;

    /// <summary>
    /// 给 <c>DeathAnimLengthOverride</c> 用：死亡时长只是在等 Spine 的死亡动画。外观上没挂 Spine 身体时
    /// （设置关闭，或骨骼没加载成功）返回 0，原版立即溶解，不会对着静止的贴图空等。
    /// </summary>
    internal static float DeathLength(MonsterModel monster, float seconds) =>
        HasSpineBody(monster) ? seconds : 0f;

    private static bool HasSpineBody(MonsterModel monster) =>
        CombatQueries.CreatureNodeOf(monster)?.Visuals switch
        {
            SpineSpriteAttackCreatureVisuals visuals => visuals.HasSpineBody,
            SceneAnimatedCreatureVisuals visuals => visuals.HasSpineBody,
            _ => false,
        };
}
