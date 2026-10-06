using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

/// <summary>
/// 小红帽雇佣兵：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，整图只平移转动，各姿势按标注点对齐），
/// 加载失败时退回逐帧换图。攻击照原来在两张挥砍间轮换，开火在四张开火姿势间轮换。
/// 布局整只水平镜像；暴怒后转身朝向玩家时骨架要跟着贴图重新对齐，才会一起镜像。
/// </summary>
public partial class LittleRedMercenaryCreatureVisuals
    : SpineSpriteAttackCreatureVisuals, INonSpineVisualTriggerHandler
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "little_red_mercenary",
        "little_red",
        "attack1",
        new Dictionary<string, string>
        {
            ["Fire1"] = "fire1",
            ["Fire2"] = "fire2",
            ["Fire3"] = "fire3",
            ["Fire4"] = "fire4",
        }) with { AttackCycle = ["attack1", "attack2"] };

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private int _fireIndex;

    [MonsterVisual(typeof(LittleRedRidingHoodedMercenary))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -138f), new(-0.71f, 0.71f), -101f, -281f, 101f, 8f, new(0f, -146f), new(13f, -349f))
    {
        TalkPos = new Vector2(18f, -310f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public void FacePlayers()
    {
        SetSpriteFlipH(true);
        RealignSpineBody();
    }

    // 重新实现触发入口：骨架的额外触发一个名字只对一段动画，"Fire" 原来按次在四张开火图间轮换，
    // 这里按次改名成 Fire1–Fire4 再交给 Spine。没有 Spine 身体时照旧走换图（换图自己轮换）。
    bool INonSpineVisualTriggerHandler.TryPlayTrigger(string triggerName)
    {
        if (!HasSpineBody)
        {
            return TryPlayTrigger(triggerName);
        }

        // 换图前的钩子照原触发名调（与基类一致），改名只给骨架用
        BeforeResolveSpriteTrigger(triggerName);
        if (triggerName == "Fire")
        {
            triggerName = $"Fire{_fireIndex++ % 4 + 1}";
        }

        return PlaySpineTrigger(triggerName);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                LittleRedMercenaryAssets.LittleRedMercenaryMonsterRoot + "idle.png")
            .At(-8f, -130f)
            .Scale(-0.74f, 0.74f)
            .AnchorX(76f)
            .IdleOnly();

        profile.Frame("attack_1", LittleRedMercenaryAssets.LittleRedMercenaryMonsterRoot + "attack_1.png")
            .Nudge(24f, -140f)
            .Scale(-0.76f, 0.76f)
            .AnchorX(274f);
        profile.Frame("attack_2", LittleRedMercenaryAssets.LittleRedMercenaryMonsterRoot + "attack_2.png")
            .Nudge(24f, -140f)
            .Scale(-0.76f, 0.76f)
            .AnchorX(438f);
        profile.Frame("fire_1", LittleRedMercenaryAssets.LittleRedMercenaryMonsterRoot + "fire_1.png")
            .Nudge(30f, -140f)
            .Scale(-0.58f, 0.58f)
            .AnchorX(570f);
        profile.Frame("fire_2", LittleRedMercenaryAssets.LittleRedMercenaryMonsterRoot + "fire_2.png")
            .Nudge(30f, -140f)
            .Scale(-0.58f, 0.58f)
            .AnchorX(614f);
        profile.Frame("fire_3", LittleRedMercenaryAssets.LittleRedMercenaryMonsterRoot + "fire_3.png")
            .Nudge(30f, -140f)
            .Scale(-0.58f, 0.58f)
            .AnchorX(525f);
        profile.Frame("fire_4", LittleRedMercenaryAssets.LittleRedMercenaryMonsterRoot + "fire_4.png")
            .Nudge(30f, -140f)
            .Scale(-0.58f, 0.58f)
            .AnchorX(1297f);
        profile.Frame("hit", LittleRedMercenaryAssets.LittleRedMercenaryMonsterRoot + "hit.png");

        profile.Lunge(
                ["attack_1", "attack_2"],
                0.18f,
                0.08f,
                0.22f,
                "Attack")
            .Cycle();
        profile.Lunge(
                ["fire_1", "fire_2", "fire_3", "fire_4"],
                0.18f,
                0.08f,
                0.22f,
                "Fire")
            .Cycle();
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
