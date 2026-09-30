using Godot;
using LibraryOfRuina.framework.visuals;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

internal static class LittleRedMercenaryBackgroundController
{
    public static void SetRageBackground(bool isRage) =>
        CombatBackgroundImage.SetTexture(
            CombatBackgroundImage.Find("LittleRedMercenaryBackgroundImage"),
            isRage ? LittleRedMercenaryAssets.Background2 : LittleRedMercenaryAssets.Background1);
}
