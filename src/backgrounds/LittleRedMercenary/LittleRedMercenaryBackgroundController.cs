using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.backgrounds.LittleRedMercenary;

internal static class LittleRedMercenaryBackgroundController
{
    private const string NormalTexturePath = "res://images/backgrounds/little_red_mercenary_elite/background_1.png";
    private const string RageTexturePath = "res://images/backgrounds/little_red_mercenary_elite/background_2.png";

    public static void SetRageBackground(bool isRage)
    {
        NCombatBackground? background = NCombatRoom.Instance?.Background;
        if (background == null)
        {
            return;
        }

        TextureRect? image = background.GetNodeOrNull<TextureRect>("%LittleRedMercenaryBackgroundImage")
            ?? background.FindChild("LittleRedMercenaryBackgroundImage", recursive: true, owned: false) as TextureRect;
        if (image == null)
        {
            return;
        }

        string texturePath = isRage ? RageTexturePath : NormalTexturePath;
        Texture2D? texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (texture != null)
        {
            image.Texture = texture;
        }
    }
}
