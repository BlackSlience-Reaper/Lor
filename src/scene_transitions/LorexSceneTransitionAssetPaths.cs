namespace LibraryOfRuina.scene_transitions;

internal static class LorexSceneTransitionAssetPaths
{
    public static IEnumerable<string> All
    {
        get
        {
            for (int i = 0; i < LorexSceneRevealOverlay.PaperVariantCount; i++)
            {
                yield return GetPaperPath(i);
                yield return GetPaperGlowPath(i);
            }
        }
    }

    public static string GetPaperPath(int index) =>
        $"res://images/vfx/scene_transitions/lorex_reveal/paper/{index}.png";

    public static string GetPaperGlowPath(int index) =>
        $"res://images/vfx/scene_transitions/lorex_reveal/paper/glow/{index}.png";
}
