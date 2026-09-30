using LibraryOfRuina.framework.visuals;

namespace LibraryOfRuina.content.guests.DawnOffice;

public abstract partial class DawnOfficeTripleAttackCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static SpriteVisualProfile BuildTripleAttackProfile(
        string texturePrefix,
        string idleTexturePath,
        float attackEndX,
        float attackEndY,
        float attackScale)
    {
        string root = $"res://images/monsters/{texturePrefix}";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            idleTexturePath);
        profile.Frame("strike", root + "_attack_strike.webp")
            .Nudge(attackEndX, attackEndY)
            .Scale(attackScale);
        profile.Frame("thrust", root + "_attack_thrust.webp")
            .Nudge(attackEndX, attackEndY)
            .Scale(attackScale);
        profile.Frame("slash", root + "_attack_slash.webp")
            .Nudge(attackEndX, attackEndY)
            .Scale(attackScale);
        profile.Frame("hit", root + "_hit.webp");
        profile.Lunge(
                ["strike", "thrust", "slash"],
                0.2f,
                0.1f,
                0.25f,
                "Attack")
            .Cycle();
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
