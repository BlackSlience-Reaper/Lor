using System;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorPhaseBossCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        SetSpriteVisualVariant(VariantKey(ResolvePhase()));
    }

    private int ResolvePhase()
    {
        return (GetParent() as NCreature)?.Entity?.Monster
            is HistoryFloorPhaseBoss boss
            ? boss.Phase
            : 1;
    }

    private static string VariantKey(int phase) =>
        $"phase_{Math.Clamp(phase, 1, 5)}";

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        for (int phase = 1; phase <= 5; phase++)
        {
            string variant = VariantKey(phase);
            string attack = variant + "_attack";
            string cast = variant + "_cast";
            string hit = variant + "_hit";
            profile.Variant(
                variant,
                HistoryFloorPhaseBoss.IdleTexturePathForPhase(phase));
            profile.Frame(
                    attack,
                    HistoryFloorPhaseBoss.AttackTexturePathForPhase(phase))
                .ForVariant(variant)
                .Nudge(28f, -116f)
                .Scale(0.60f);
            profile.Frame(
                cast,
                HistoryFloorPhaseBoss.CastTexturePathForPhase(phase))
                .ForVariant(variant);
            profile.Frame(
                hit,
                HistoryFloorPhaseBoss.HitTexturePathForPhase(phase))
                .ForVariant(variant);
            profile.Lunge(
                    attack,
                    0.22f,
                    0.12f,
                    0.25f,
                    "Attack")
                .ForVariant(variant);
            profile.Swap(cast, 0.45f, "Cast", "Parry")
                .ForVariant(variant);
            profile.Swap(hit, 0.12f, "Hit")
                .ForVariant(variant);
        }

        profile.InitialVariant(VariantKey(1));
        return profile;
    }
}
