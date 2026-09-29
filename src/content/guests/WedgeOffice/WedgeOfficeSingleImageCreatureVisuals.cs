using LibraryOfRuina.framework.visuals;

namespace LibraryOfRuina.content.guests.WedgeOffice;

public abstract partial class WedgeOfficeSingleImageCreatureVisuals : SpriteAttackCreatureVisuals
{
    private const string FallbackTexturePath = "res://images/monsters/philip.png";

    private SpriteVisualProfile? _profile;

    protected virtual string SharedTexturePath => FallbackTexturePath;

    protected abstract float AttackEndX { get; }

    protected abstract float AttackEndY { get; }

    protected abstract float AttackEndScale { get; }

    internal override SpriteVisualProfile SpriteProfile =>
        _profile ??= BuildProfile();

    private SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            SharedTexturePath);
        profile.Frame("attack", SharedTexturePath)
            .Nudge(AttackEndX, AttackEndY)
            .Scale(AttackEndScale);
        profile.Frame("hit", SharedTexturePath);
        profile.Lunge("attack", 0.2f, 0.08f, 0.24f, "Attack");
        profile.Swap("hit", 0.1f, "Hit");
        return profile;
    }
}
