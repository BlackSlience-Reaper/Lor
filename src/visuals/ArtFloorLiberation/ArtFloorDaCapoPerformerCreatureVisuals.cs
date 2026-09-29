using System;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.ArtFloorLiberation;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class ArtFloorDaCapoPerformerCreatureVisuals : SpriteAttackCreatureVisuals
{
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
            : "res://images/monsters/art_floor/dacapo_performers/performer_1_idle.png";
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
