using Godot;
using LibraryOfRuina.framework.visuals;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

internal static class LittleRedMercenaryBackgroundController
{
    private const string NormalTexturePath = "res://images/backgrounds/little_red_mercenary_elite/background_1.png";
    private const string RageTexturePath = "res://images/backgrounds/little_red_mercenary_elite/background_2.png";

    public static void SetRageBackground(bool isRage) =>
        CombatBackgroundImage.SetTexture(
            CombatBackgroundImage.Find("LittleRedMercenaryBackgroundImage"),
            isRage ? RageTexturePath : NormalTexturePath);
}
