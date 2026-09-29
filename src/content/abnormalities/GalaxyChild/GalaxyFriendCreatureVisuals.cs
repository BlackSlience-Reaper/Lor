using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.ArtFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.GalaxyChild;

public sealed partial class GalaxyFriendCreatureVisuals : SpriteAttackCreatureVisuals
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
