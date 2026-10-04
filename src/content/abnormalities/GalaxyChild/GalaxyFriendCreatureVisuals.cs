using System.Collections.Generic;
using Godot;
using LibraryOfRuina.content.liberation.Art;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.GalaxyChild;

/// <summary>
/// 银河之友（及艺术层的同名怪物）的外观：Spine 身体（tools/spine_from_sprite/galaxy_friend.json 生成）。待机时脖子摆动、
/// 整团起伏；星光坠落、等待（格挡与闪烁共用）换成原图的人物，受击换成原受击图，死亡时脖子软软趴到地上。
/// 假死不移除节点，原版也不会发死亡触发，所以假死时仍站着，与之前一致。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public sealed partial class GalaxyFriendCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(GalaxyFriend))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -110f), new(0.55f, 0.55f), -122f, -278f, 122f, 8f, new(0f, -112f), new(0f, -318f))
    {
        TalkPos = new Vector2(0f, -242f),
    };

    // 艺术层版本只有意图图标位置不同（低 23）。
    [MonsterVisual(typeof(ArtFloorGalaxyFriend))]
    internal static readonly CreatureVisualLayout ArtFloorLayout = new(
        new(0f, -110f), new(0.55f, 0.55f), -122f, -278f, 122f, 8f, new(0f, -112f), new(0f, -295f))
    {
        TalkPos = new Vector2(0f, -242f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后第一束星光命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>星光坠落后续各段的等待，秒。三段约在 0.3、0.5、0.7 秒落下，和人物鼓起的时刻对齐，都在攻击姿势里（0.88 秒后才换回）。</summary>
    internal const float FollowUpHitSeconds = 0.2f;

    /// <summary>死亡动画时长，秒；原版等它播完再做溶解消失。</summary>
    internal const float DeathSeconds = 1.6f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        GalaxyChildAssets.GalaxyFriendSpineAtlas,
        GalaxyChildAssets.GalaxyFriendSpineSkeleton,
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        DeathAnimation: "die",
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string>
        {
            ["Parry"] = "parry",
            ["Cast"] = "parry",
        });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            GalaxyFriend.IdleTexturePath);
        profile.Frame("attack", GalaxyFriend.AttackTexturePath);
        profile.Frame("hit", GalaxyFriend.HitTexturePath);
        profile.Frame("parry", GalaxyFriend.ParryTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("parry", 0.42f, "Parry", "Cast");
        return profile;
    }
}
