using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorVineBarrierCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HistoryFloorVineBarrier))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -88f), new(0.40f, 0.40f), -94f, -198f, 94f, 8f, new(0f, -92f), new(0f, -236f))
    {
        TalkPos = new Vector2(0f, -188f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HistoryFloorVineBarrier.IdleTexturePath)
            .At(0f, -88f)
            .Scale(0.4f)
            .IdleOnly();
        string idle =
            $"@idle:{SpriteVisualProfile.DefaultVariantKey}";
        profile.Swap(idle, 0.35f, "Defend", "Cast");
        profile.Swap(idle, 0.14f, "Hit");
        return profile;
    }
}
