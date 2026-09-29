using System;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.History;

public partial class HistoryFloorPhaseBossCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HistoryFloorPhaseBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -118f), new(0.58f, 0.58f), -155f, -310f, 155f, 10f, new(0f, -120f), new(0f, -340f))
    {
        TalkPos = new Vector2(0f, -260f),
    };

    // 初始立绘按当前阶段取；_Ready 里再按阶段切到对应变体。
    [MonsterVisualFactory]
    internal static NCreatureVisuals CreateForMonster(MonsterModel monster)
    {
        string id = monster.Id.Entry;
        int phase = monster is HistoryFloorPhaseBoss boss ? boss.Phase : 1;
        return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorPhaseBossCreatureVisuals>(
            id,
            HistoryFloorPhaseBoss.IdleTexturePathForPhase(phase));
    }

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
