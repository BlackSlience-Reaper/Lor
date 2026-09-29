using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.SpinyBus;

public sealed partial class SpinyBusCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.SpinyBus.SpinyBus))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -126f), new(0.58f, 0.58f), -132f, -320f, 132f, 10f, new(0f, -126f), new(0f, -348f))
    {
        TalkPos = new Vector2(0f, -266f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    protected override void BeforeResolveSpriteTrigger(string triggerName)
    {
        if (triggerName == "Hit")
        {
            LocalOggOneShotPlayer.Play(
                monsters.SpinyBus.SpinyBus.HitSfxPath,
                -2f);
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            monsters.SpinyBus.SpinyBus.IdleTexturePath);
        profile.Frame(
            "parry",
            monsters.SpinyBus.SpinyBus.ParryTexturePath);
        profile.Frame(
            "attack",
            monsters.SpinyBus.SpinyBus.AttackTexturePath);
        profile.Frame(
            "attack2",
            monsters.SpinyBus.SpinyBus.Attack2TexturePath);
        profile.Frame(
            "hit",
            monsters.SpinyBus.SpinyBus.HitTexturePath);
        profile.Swap("parry", 0.50f, "Parry");
        profile.Swap("attack", 0.48f, "Attack");
        profile.Swap("attack2", 0.48f, "Attack2");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}
