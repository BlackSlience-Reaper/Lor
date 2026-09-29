using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.liberation.History;

public partial class HistoryFloorEmeraldBoughCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HistoryFloorEmeraldBoughBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -128f), new(0.50f, 0.50f), -170f, -315f, 170f, 24f, new(0f, -126f), new(0f, -342f))
    {
        TalkPos = new Vector2(0f, -268f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HistoryFloorEmeraldBoughBoss.IdleTexturePath)
            .At(0f, -128f)
            .Scale(0.5f)
            .IdleOnly();
        profile.Frame(
                "attack",
                HistoryFloorEmeraldBoughBoss.AttackTexturePath)
            .Nudge(26f, -128f)
            .Scale(0.56f);
        profile.Frame(
            "hit",
            HistoryFloorEmeraldBoughBoss.HitTexturePath);
        profile.Frame(
            "parry",
            HistoryFloorEmeraldBoughBoss.ParryTexturePath);
        profile.Frame(
            "ego",
            HistoryFloorEmeraldBoughBoss.EgoTexturePath);
        profile.Lunge("attack", 0.22f, 0.12f, 0.25f, "Attack");
        profile.Swap("parry", 0.55f, "Cast", "Parry");
        profile.Swap("ego", 0.55f, "Ego");
        profile.Swap("hit", 0.14f, "Hit");
        return profile;
    }
}
