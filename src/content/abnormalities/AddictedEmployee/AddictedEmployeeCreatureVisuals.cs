using Godot;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.AddictedEmployee;

public sealed partial class AddictedEmployeeCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(AddictedEmployee))]
    [MonsterVisual(typeof(TechnologyFloorChordStaff))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -122f), new(0.58f, 0.58f), -96f, -230f, 99f, -20f, new(0f, -122f), new(20f, -290f))
    {
        TalkPos = new Vector2(0f, -286f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            AddictedEmployeeAssets.AddictedEmployeeMonsterPrefix + "idle.png");
        profile.Frame("attack", AddictedEmployeeAssets.AddictedEmployeeMonsterPrefix + "attack.png");
        profile.Frame("guard", AddictedEmployeeAssets.AddictedEmployeeMonsterPrefix + "guard.png");
        profile.Frame("hit", AddictedEmployeeAssets.AddictedEmployeeMonsterPrefix + "hit.png");
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("guard", 0.40f, "Guard");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

