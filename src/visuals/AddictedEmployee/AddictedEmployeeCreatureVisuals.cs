using Godot;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.AddictedEmployee;

public sealed partial class AddictedEmployeeCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.AddictedEmployee.AddictedEmployee))]
    [MonsterVisual(typeof(TechnologyFloorChordStaff))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -122f), new(0.58f, 0.58f), -118f, -330f, 118f, 12f, new(0f, -122f), new(20f, -290f))
    {
        TalkPos = new Vector2(0f, -286f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root =
            "res://images/monsters/addicted_employee/addicted_employee_";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + "idle.png");
        profile.Frame("attack", root + "attack.png");
        profile.Frame("guard", root + "guard.png");
        profile.Frame("hit", root + "hit.png");
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("guard", 0.40f, "Guard");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

