using System;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Art;

/// <summary>
/// Da Capo 的四种演奏者：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，只平移、转动），
/// 每种演奏者（贴图外观的 Variant）一副骨架，加载失败时退回逐帧换图。
/// </summary>
public sealed partial class ArtFloorDaCapoPerformerCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    private static RuntimeSpineBody.Spec CreateSpine(int index) => LayeredBossSpine.Create(
        "art_floor",
        $"dacapo_performer_{index}",
        "attack",
        new Dictionary<string, string>
        {
            ["Guard"] = "guard",
        });

    internal static readonly RuntimeSpineBody.Spec Performer1Spine = CreateSpine(1);
    internal static readonly RuntimeSpineBody.Spec Performer2Spine = CreateSpine(2);
    internal static readonly RuntimeSpineBody.Spec Performer3Spine = CreateSpine(3);
    internal static readonly RuntimeSpineBody.Spec Performer4Spine = CreateSpine(4);

    internal override RuntimeSpineBody.Spec SpineSpec => Performer1Spine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        [Performer1Spine, Performer2Spine, Performer3Spine, Performer4Spine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) => variantKey switch
    {
        "performer_2" => Performer2Spine,
        "performer_3" => Performer3Spine,
        "performer_4" => Performer4Spine,
        _ => Performer1Spine,
    };

    [MonsterVisual(typeof(ArtFloorDaCapoPerformer))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -48f), new(0.48f, 0.48f), -88f, -260f, 88f, 10f, new(0f, -136f), new(0f, -298f))
    {
        TalkPos = new Vector2(0f, -228f),
        StateDisplayLiftY = 30f,
    };

    // 初始立绘取演奏者自己的变体。
    [MonsterVisualFactory]
    internal static NCreatureVisuals CreateForMonster(MonsterModel monster)
    {
        string id = monster.Id.Entry;
        string idleTexturePath = monster is ArtFloorDaCapoPerformer performer
            ? performer.IdleTexturePath
            : ArtFloorAssets.Performer1IdleTexture;
        return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArtFloorDaCapoPerformerCreatureVisuals>(
            id,
            idleTexturePath);
    }

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        if (GetParent() is not NCreature creatureNode
            || creatureNode.Entity?.Monster is not ArtFloorDaCapoPerformer performer)
        {
            throw new InvalidOperationException("ArtFloorDaCapoPerformerCreatureVisuals requires ArtFloorDaCapoPerformer.");
        }

        foreach (SpriteVisualVariantDefinition variant in Profile.Variants.Values)
        {
            if (variant.IdleTexturePath == performer.IdleTexturePath)
            {
                SetSpriteVisualVariant(variant.Key);
                return;
            }
        }

        throw new InvalidOperationException(
            $"No sprite profile variant matches '{performer.IdleTexturePath}'.");
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        for (int index = 1; index <= 4; index++)
        {
            string variant = $"performer_{index}";
            string prefix = ArtFloorDaCapoPerformer.Root + variant;
            string attack = variant + "_attack";
            string hit = variant + "_hit";
            string guard = variant + "_guard";

            profile.Variant(variant, prefix + "_idle.png");
            profile.Frame(attack, prefix + "_attack.png")
                .ForVariant(variant);
            profile.Frame(hit, prefix + "_hit.png")
                .ForVariant(variant);
            profile.Frame(guard, prefix + "_guard.png")
                .ForVariant(variant);
            profile.Swap(attack, 0.45f, "Attack").ForVariant(variant);
            profile.Swap(hit, 0.40f, "Hit").ForVariant(variant);
            profile.Swap(guard, 0.45f, "Guard").ForVariant(variant);
        }

        profile.InitialVariant("performer_1");
        return profile;
    }
}
