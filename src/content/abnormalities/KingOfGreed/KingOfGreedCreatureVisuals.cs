using System;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.KingOfGreed;

[MonsterVisual(typeof(KingOfGreed), ScenePath = KingOfGreedCreatureVisuals.ScenePath)]
internal sealed partial class KingOfGreedCreatureVisuals
    : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        KingOfGreedAssets.KingOfGreedScene;

    private int _magicalGirlAttackCursor;
    private int _kingAttackCursor;
    private bool _fallbackKingForm;

    protected override string ResolveCurrentAnimationLibrary() =>
        IsKingForm()
            ? KingOfGreedAnimationContract.KingLibrary
            : KingOfGreedAnimationContract.MagicalGirlLibrary;

    protected override string NormalizeTriggerName(string triggerName)
    {
        if (triggerName != "Attack")
        {
            return triggerName;
        }

        if (IsKingForm())
        {
            return _kingAttackCursor++ % 2 == 0
                ? KingOfGreedAnimationContract.AttackStabTrigger
                : KingOfGreedAnimationContract.AttackSlashTrigger;
        }

        return _magicalGirlAttackCursor++ % 2 == 0
            ? KingOfGreedAnimationContract.AttackStabTrigger
            : KingOfGreedAnimationContract.AttackSlashTrigger;
    }

    public void SetKingForm(bool isKingForm)
    {
        _fallbackKingForm = isKingForm;
        TryPlayTrigger("Idle");
    }

    internal static string ResolveAnimationName(
        bool kingForm,
        string triggerName) =>
        $"{KingOfGreedAnimationContract.LibraryForForm(kingForm)}/{triggerName}";

    private bool IsKingForm() =>
        (GetParent() as NCreature)?.Entity?.Monster
            is KingOfGreed boss
                ? !boss.IsMagicalGirl
                : _fallbackKingForm;
}

internal static class KingOfGreedAnimationContract
{
    internal const string MagicalGirlLibrary = "magical_girl";
    internal const string KingLibrary = "king";
    internal const string AttackStabTrigger = "AttackStab";
    internal const string AttackSlashTrigger = "AttackSlash";

    internal const float AttackDurationSeconds = 0.48f;
    internal const float SpecialDurationSeconds = 1.95f;
    internal const float SpecialSegmentDurationSeconds = 1f;
    internal const float HitDurationSeconds = 0.12f;

    internal static IReadOnlyList<string> Libraries { get; } =
        [MagicalGirlLibrary, KingLibrary];

    internal static IReadOnlyList<string> Animations { get; } =
    [
        "Idle",
        AttackStabTrigger,
        AttackSlashTrigger,
        "Special",
        "SpecialIntro",
        "SpecialAttack",
        "Hit"
    ];

    internal static string LibraryForForm(bool kingForm) =>
        kingForm ? KingLibrary : MagicalGirlLibrary;

    internal static float DurationForTrigger(string triggerName) =>
        triggerName switch
        {
            AttackStabTrigger or AttackSlashTrigger => AttackDurationSeconds,
            "Special" => SpecialDurationSeconds,
            "SpecialIntro" or "SpecialAttack" =>
                SpecialSegmentDurationSeconds,
            "Hit" => HitDurationSeconds,
            _ => throw new ArgumentOutOfRangeException(
                nameof(triggerName),
                triggerName,
                "Only non-idle King of Greed actions have a duration.")
        };
}

public partial class GoldenAmberCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(GoldenAmber))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 0f), new(0.48f, 0.48f), -155f, -430f, 155f, 12f, new(0f, -215f), new(0f, -465f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                GoldenAmber.IdleTexturePath)
            .Scale(0.48f)
            .IdleOnly();
        profile.Swap(
            $"@idle:{SpriteVisualProfile.DefaultVariantKey}",
            0.12f,
            "Hit");
        return profile;
    }
}

public partial class ShiningHappinessCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ShiningHappiness))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 0f), new(0.50f, 0.50f), -82f, -188f, 82f, 8f, new(0f, -88f), new(0f, -222f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                ShiningHappiness.IdleTexturePath)
            .Scale(0.50f)
            .IdleOnly();
        profile.Swap(
            $"@idle:{SpriteVisualProfile.DefaultVariantKey}",
            0.12f,
            "Hit");
        return profile;
    }
}
