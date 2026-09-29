using Godot;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.FuneralOfTheDeadButterflies;

public partial class FuneralOfTheDeadButterfliesCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -126f), new(0.58f, 0.58f), -165f, -315f, 165f, 12f, new(0f, -126f), new(0f, -350f))
    {
        TalkPos = new Vector2(0f, -278f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.IdleTexturePath);
        profile.Frame(
            "coffin_prepare",
            monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.CoffinPrepareTexturePath);
        profile.Frame(
            "coffin_attack",
            monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.CoffinAttackTexturePath);
        profile.Frame(
            "fire_black",
            monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.FireBlackTexturePath);
        profile.Frame(
            "fire_white",
            monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.FireWhiteTexturePath);
        profile.Frame(
            "hit",
            monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.HitTexturePath);
        profile.Swap(
            "fire_black",
            0.22f * 2.25f,
            "Attack",
            "AttackBlack");
        profile.Swap("fire_white", 0.26f * 2.25f, "AttackWhite");
        profile.Swap("coffin_prepare", 0.24f * 2.25f, "Cast");
        profile.Swap(
            "coffin_attack",
            0.24f * 2.25f,
            "CoffinAttack");
        profile.Swap("hit", 0.16f * 2.25f, "Hit");
        return profile;
    }
}
