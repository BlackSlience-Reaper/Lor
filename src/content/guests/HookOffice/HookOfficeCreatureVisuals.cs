using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.HookOffice;

public abstract partial class HookOfficeCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static SpriteVisualProfile BuildProfile(
        string texturePrefix,
        string idleTexturePath,
        IReadOnlyList<string> attackSuffixes,
        float attackEndX,
        float attackEndY,
        float attackScale)
    {
        string root = $"res://images/monsters/{texturePrefix}";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            idleTexturePath);
        var frameKeys = new string[attackSuffixes.Count];
        for (int i = 0; i < attackSuffixes.Count; i++)
        {
            string frameKey = $"attack_{i + 1}";
            frameKeys[i] = frameKey;
            profile.Frame(
                    frameKey,
                    $"{root}_attack_{attackSuffixes[i]}.webp")
                .Nudge(attackEndX, attackEndY)
                .Scale(attackScale);
        }

        profile.Frame("hit", root + "_hit.webp");
        profile.Lunge(
                frameKeys,
                0.2f,
                0.1f,
                0.25f,
                "Attack")
            .Cycle();
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

public partial class TaeinCreatureVisuals : HookOfficeCreatureVisuals
{
    [MonsterVisual(typeof(Taein))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145.2f), new(0.48f, 0.48f), -124f, -299.7f, 124f, 5f, new(0f, -139.8f), new(0f, -333.7f))
    {
        TalkPos = new Vector2(0f, -262f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildProfile(
            "taein",
            "res://images/monsters/taein.webp",
            ["thrust", "strike", "slash"],
            18f,
            -145.2f,
            0.50f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}

public partial class MccullinCreatureVisuals : HookOfficeCreatureVisuals
{
    [MonsterVisual(typeof(Mccullin))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145.2f), new(0.49f, 0.49f), -108f, -299.7f, 108f, 5f, new(0f, -139.8f), new(0f, -333.7f))
    {
        TalkPos = new Vector2(0f, -262f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildProfile(
            "mccullin",
            "res://images/monsters/mccullin.webp",
            ["strike", "thrust", "slash"],
            18f,
            -145.2f,
            0.50f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}

public partial class NaokiCreatureVisuals : HookOfficeCreatureVisuals
{
    [MonsterVisual(typeof(Naoki))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145.2f), new(0.50f, 0.50f), -104f, -299.7f, 104f, 5f, new(0f, -139.8f), new(0f, -333.7f))
    {
        TalkPos = new Vector2(0f, -262f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildProfile(
            "naoki",
            "res://images/monsters/naoki.webp",
            ["thrust", "strike", "slash"],
            18f,
            -145.2f,
            0.50f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
