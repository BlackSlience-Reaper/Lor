using Godot;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.MusiciansOfBremen;

/// <summary>姆姆的外观：只有一张待机图，攻击、受击都是骨骼动作，见 <see cref="GuestSpine"/>。</summary>
public partial class MuMuCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(MuMu))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -146f), new(0.46f, 0.46f), -138f, -299.7f, 138f, 5f, new(0f, -139.8f), new(0f, -333.7f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine =
        GuestSpine.Create("", "mu_mu", GuestSpine.StrikeThrustSlash);

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(SpriteVisualProfile.DefaultVariantKey, "res://images/monsters/mu_mu.webp");
        return profile;
    }
}
