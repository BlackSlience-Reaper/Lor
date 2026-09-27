namespace LibraryOfRuina.visuals.AddictedEmployee;

public sealed partial class AddictedEmployeeCreatureVisuals : SpriteAttackCreatureVisuals
{
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

