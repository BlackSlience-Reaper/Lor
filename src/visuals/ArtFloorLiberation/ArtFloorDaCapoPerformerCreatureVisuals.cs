using System;
using LibraryOfRuina.monsters.ArtFloorLiberation;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class ArtFloorDaCapoPerformerCreatureVisuals : SpriteAttackCreatureVisuals
{
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
