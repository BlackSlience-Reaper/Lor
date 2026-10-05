using LibraryOfRuina.core.settings;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.framework.visuals;

/// <summary>设置“战斗界面 / 动画效果”的三档。值名即本地化键后缀，不能改名。</summary>
public enum AnimationEffectLevel
{
    /// <summary>逐帧换图，瞬间切换（Spine 化之前的样子）。</summary>
    Off,

    /// <summary>逐帧换图，待机图与动作图之间淡入淡出（<see cref="SpriteCrossfade"/>）。</summary>
    Low,

    /// <summary>原版分层生成的 Spine 骨骼动画（<see cref="RuntimeSpineBody"/>），加载失败的怪物退回逐帧换图。</summary>
    High,
}

/// <summary>
/// “动画效果”设置的读取入口。只影响本机外观，不参与联机比对。外观在怪物出场时按当时的档位建立
/// （Spine 身体与淡入淡出组件都只在出场时挂上），局内切换对之后出场的怪物生效。
/// </summary>
internal static class AnimationEffects
{
    internal static bool SpineEnabled => LibraryOfRuinaSettings.AnimationEffectLevel == AnimationEffectLevel.High;

    internal static bool CrossfadeEnabled => LibraryOfRuinaSettings.AnimationEffectLevel == AnimationEffectLevel.Low;

    /// <summary>
    /// 给 <c>DeathAnimLengthOverride</c> 用：死亡时长只是在等 Spine 的死亡动画。外观上没挂 Spine 身体时
    /// （关、低两档，或骨骼没加载成功）返回 0，原版立即溶解，不会对着静止的贴图空等。
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
