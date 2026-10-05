using Godot;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.DawnOffice;

/// <summary>芬恩的外观：贴图外观只有一张待机图；Spine 身体用原版分层立绘，攻击、受击换成原版对应姿势，见 <see cref="GuestSpine"/>。</summary>
public partial class FinnCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(Finn))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145f), new(0.42f, 0.42f), -105f, -299.7f, 105f, 5f, new(0f, -139.8f), new(0f, -333.7f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine =
        GuestSpine.Create("", "finn", GuestSpine.StrikeThrustSlash);

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(SpriteVisualProfile.DefaultVariantKey, "res://images/monsters/finn.png");
        return profile;
    }
}
